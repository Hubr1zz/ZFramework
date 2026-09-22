using System.Collections.Generic;

namespace HuntingInDarkness.GameCore.Content
{
    public interface IStableContentRecord
    {
        string Id { get; }
    }

    public interface IContentTableSource<out TRecord> where TRecord : IStableContentRecord
    {
        IReadOnlyList<TRecord> Load();
    }
}
