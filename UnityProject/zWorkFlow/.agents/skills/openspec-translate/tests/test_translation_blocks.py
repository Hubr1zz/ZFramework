import sys
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPT_DIR))

import translation_blocks as blocks  # noqa: E402


def markdown(value: str):
    return blocks.markdown_blocks(value)


def recorded_entry(source_blocks, target_blocks, translated_count=None):
    translated_count = len(target_blocks) if translated_count is None else translated_count
    target_by_id = {block["id"]: block for block in target_blocks}
    return {
        "blocks": [
            {
                "id": block["id"],
                "kind": block["kind"],
                "sourceHash": blocks.sha256(block["text"]),
                "translatedHash": (
                    blocks.sha256(target_by_id[block["id"]]["text"])
                    if index < translated_count and block["id"] in target_by_id
                    else ""
                ),
            }
            for index, block in enumerate(source_blocks)
        ]
    }


class TranslationBlockTests(unittest.TestCase):
    def test_partial_translation_reports_every_unsynchronized_block(self):
        source = markdown("# 标题\n\n第一段。\n\n第二段。\n")
        partial_target = markdown("# Title\n")
        entry = recorded_entry(source, partial_target, translated_count=1)

        changed = blocks.changed_block_ids(source, partial_target, entry)

        self.assertEqual([block["id"] for block in source[1:]], changed)

    def test_complete_translation_reuses_every_block(self):
        source = markdown("# 标题\n\n正文。\n")
        target = markdown("# Title\n\nBody.\n")
        entry = recorded_entry(source, target)

        changed = blocks.changed_block_ids(source, target, entry)

        self.assertEqual([], changed)

    def test_modified_target_block_is_reported(self):
        source = markdown("# 标题\n\n正文。\n")
        target = markdown("# Title\n\nBody.\n")
        entry = recorded_entry(source, target)
        modified_target = markdown("# Different title\n\nBody.\n")

        changed = blocks.changed_block_ids(source, modified_target, entry)

        self.assertEqual(["md:0001"], changed)

    def test_partial_target_fails_file_structure_validation(self):
        source = markdown("# 标题\n\n正文。\n")
        partial_target = markdown("# Title\n")

        issues = blocks.translation_structure_issues(source, partial_target)

        self.assertTrue(issues)
        self.assertTrue(any("block count differs" in issue for issue in issues))

    def test_trailing_blank_lines_do_not_create_translation_work(self):
        source = markdown("# 标题\n\n正文。\n\n")
        target = markdown("# Title\n\nBody.\n")
        entry = recorded_entry(source, target)

        self.assertEqual([], blocks.translation_structure_issues(source, target))
        self.assertEqual([], blocks.changed_block_ids(source, target, entry))


if __name__ == "__main__":
    unittest.main()
