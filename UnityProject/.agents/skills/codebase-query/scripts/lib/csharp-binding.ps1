# codebase-query-binding-library
Set-StrictMode -Version Latest

function Remove-CSharpTrivia {
    param([string]$Text)

    $pattern = '(?s)//[^\r\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|''(?:\\.|[^''\\])*'''
    return [regex]::Replace($Text, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        return [regex]::Replace($match.Value, '[^\r\n]', ' ')
    })
}

function Split-CSharpTypeList {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return @() }
    $items = [System.Collections.Generic.List[string]]::new()
    $start = 0
    $depth = 0
    for ($i = 0; $i -lt $Text.Length; $i++) {
        switch ($Text[$i]) {
            '<' { $depth++ }
            '>' { if ($depth -gt 0) { $depth-- } }
            ',' {
                if ($depth -eq 0) {
                    $value = $Text.Substring($start, $i - $start).Trim()
                    if ($value) { $items.Add($value) }
                    $start = $i + 1
                }
            }
        }
    }
    $last = $Text.Substring($start).Trim()
    if ($last) { $items.Add($last) }
    return @($items)
}

function Get-SimpleCSharpTypeName {
    param([string]$TypeName)

    if ([string]::IsNullOrWhiteSpace($TypeName)) { return $null }
    $value = $TypeName.Trim() -replace '^global::', ''
    $value = $value -replace '\?$', '' -replace '(\[\])+$', ''
    $genericIndex = $value.IndexOf('<')
    if ($genericIndex -ge 0) { $value = $value.Substring(0, $genericIndex) }
    if ($value.Contains('.')) { $value = ($value -split '\.')[-1] }
    return $value.Trim()
}

function Get-MatchingCSharpBrace {
    param(
        [string]$Text,
        [int]$OpenIndex
    )

    if ($OpenIndex -lt 0 -or $OpenIndex -ge $Text.Length -or $Text[$OpenIndex] -ne '{') {
        return -1
    }
    $depth = 0
    for ($i = $OpenIndex; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq '{') { $depth++ }
        elseif ($Text[$i] -eq '}') {
            $depth--
            if ($depth -eq 0) { return $i }
        }
    }
    return -1
}

function Resolve-CSharpTypeName {
    param(
        [string]$RawType,
        [object]$Record,
        [hashtable]$TypesBySimpleName,
        [hashtable]$TypesByQualifiedName
    )

    $simpleName = Get-SimpleCSharpTypeName -TypeName $RawType
    if (-not $simpleName) { return @() }

    $rawBase = ($RawType.Trim() -replace '^global::', '')
    $genericIndex = $rawBase.IndexOf('<')
    if ($genericIndex -ge 0) { $rawBase = $rawBase.Substring(0, $genericIndex) }
    $rawBase = $rawBase -replace '\?$', '' -replace '(\[\])+$', ''

    $resolved = [System.Collections.Generic.List[string]]::new()
    if ($TypesByQualifiedName.ContainsKey($rawBase)) {
        $resolved.Add($rawBase)
    }

    foreach ($alias in @($Record.aliases)) {
        if ($alias.name -eq $simpleName) {
            $aliasTargets = Resolve-CSharpTypeName -RawType $alias.target -Record $Record `
                -TypesBySimpleName $TypesBySimpleName -TypesByQualifiedName $TypesByQualifiedName
            foreach ($target in $aliasTargets) { $resolved.Add($target) }
        }
    }

    foreach ($namespace in @($Record.namespaces)) {
        $candidate = "$namespace.$simpleName"
        if ($TypesByQualifiedName.ContainsKey($candidate)) { $resolved.Add($candidate) }
    }
    foreach ($usingNamespace in @($Record.usings)) {
        $candidate = "$usingNamespace.$simpleName"
        if ($TypesByQualifiedName.ContainsKey($candidate)) { $resolved.Add($candidate) }
    }

    if ($resolved.Count -eq 0 -and $TypesBySimpleName.ContainsKey($simpleName)) {
        foreach ($candidate in @($TypesBySimpleName[$simpleName])) { $resolved.Add($candidate.qualifiedName) }
    }
    return @($resolved | Sort-Object -Unique)
}

function Find-CSharpMethodOwner {
    param(
        [string]$TypeName,
        [string]$MethodName,
        [hashtable]$TypeByQualifiedName,
        [hashtable]$MethodsByType,
        [hashtable]$Visited
    )

    if (-not $TypeName -or $Visited.ContainsKey($TypeName)) { return $null }
    $Visited[$TypeName] = $true
    if ($MethodsByType.ContainsKey($TypeName) -and @($MethodsByType[$TypeName]) -contains $MethodName) {
        return $TypeName
    }
    if (-not $TypeByQualifiedName.ContainsKey($TypeName)) { return $null }
    foreach ($baseType in @($TypeByQualifiedName[$TypeName].baseTypes)) {
        $owner = Find-CSharpMethodOwner -TypeName $baseType -MethodName $MethodName `
            -TypeByQualifiedName $TypeByQualifiedName -MethodsByType $MethodsByType -Visited $Visited
        if ($owner) { return $owner }
    }
    return $null
}

function Add-CSharpTypeBindings {
    param(
        [System.Collections.IEnumerable]$Records,
        [string]$ProjectRoot
    )

    $recordTexts = @{}
    foreach ($record in $Records) {
        $fullPath = Resolve-IndexedPath -ProjectRoot $ProjectRoot -RelativePath $record.path
        $text = Get-Content -Raw -LiteralPath $fullPath -Encoding utf8
        $code = Remove-CSharpTrivia -Text $text
        $recordTexts[$record.path] = $code

        $usings = @([regex]::Matches($code, '(?m)^\s*using\s+(?!static\s)([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        $aliases = @([regex]::Matches($code, '(?m)^\s*using\s+([A-Za-z_]\w*)\s*=\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;') |
            ForEach-Object { [pscustomobject]@{ name = $_.Groups[1].Value; target = $_.Groups[2].Value } })

        $namespaceName = @($record.namespaces | Select-Object -First 1)
        $typePattern = '(?m)^\s*(?:(?:public|internal|protected|private|static|abstract|sealed|partial|readonly|ref|new)\s+)*(class|interface|struct|enum|record)\s+([A-Za-z_]\w*)(?:\s*<[^{}\r\n]+>)?\s*(?::\s*([^\r\n{]+))?\s*\{'
        $types = @([regex]::Matches($code, $typePattern) | ForEach-Object {
            $name = $_.Groups[2].Value
            $qualifiedName = if ($namespaceName.Count -gt 0) { "$($namespaceName[0]).$name" } else { $name }
            $openIndex = $_.Index + $_.Value.LastIndexOf('{')
            [pscustomobject]@{
                kind = $_.Groups[1].Value
                name = $name
                qualifiedName = $qualifiedName
                rawBaseTypes = @(Split-CSharpTypeList -Text $_.Groups[3].Value)
                baseTypes = @()
                bodyStart = $openIndex
                bodyEnd = Get-MatchingCSharpBrace -Text $code -OpenIndex $openIndex
            }
        })

        $methodDefinitions = [System.Collections.Generic.List[object]]::new()
        $methodPattern = '(?m)^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|extern|new|partial|unsafe)\s+)+([A-Za-z_][A-Za-z0-9_<>,\[\].?]*)\s+([A-Za-z_]\w*)(?:\s*<[^;{}()]+>)?\s*\('
        foreach ($match in [regex]::Matches($code, $methodPattern)) {
            $containingTypes = @($types | Where-Object {
                $_.bodyStart -lt $match.Index -and $_.bodyEnd -ge $match.Index
            } | Sort-Object { $_.bodyEnd - $_.bodyStart })
            $declaringType = if ($containingTypes.Count -gt 0) { $containingTypes[0].qualifiedName } else { $null }
            $methodDefinitions.Add([pscustomobject]@{
                name = $match.Groups[2].Value
                declaringType = $declaringType
                qualifiedName = if ($declaringType) { "$declaringType.$($match.Groups[2].Value)" } else { $match.Groups[2].Value }
                returnType = $match.Groups[1].Value
            })
        }
        foreach ($type in $types) {
            $constructorPattern = '(?m)^\s*(?:(?:public|private|protected|internal|static|extern|unsafe)\s+)+' + [regex]::Escape($type.name) + '\s*\('
            foreach ($match in [regex]::Matches($code, $constructorPattern)) {
                if ($match.Index -gt $type.bodyStart -and $match.Index -le $type.bodyEnd) {
                    $methodDefinitions.Add([pscustomobject]@{
                        name = $type.name
                        declaringType = $type.qualifiedName
                        qualifiedName = "$($type.qualifiedName).$($type.name)"
                        returnType = $type.qualifiedName
                    })
                }
            }
        }

        $record.types = @($types)
        $record.methods = @($methodDefinitions | ForEach-Object { $_.name } | Sort-Object -Unique)
        $record | Add-Member -NotePropertyName methodDefinitions -NotePropertyValue @($methodDefinitions) -Force
        $record | Add-Member -NotePropertyName usings -NotePropertyValue $usings -Force
        $record | Add-Member -NotePropertyName aliases -NotePropertyValue $aliases -Force
        $record | Add-Member -NotePropertyName qualifiedTypeReferences -NotePropertyValue @() -Force
        $record | Add-Member -NotePropertyName resolvedCalls -NotePropertyValue @() -Force
    }

    $typesBySimpleName = @{}
    $typesByQualifiedName = @{}
    $methodsByType = @{}
    foreach ($record in $Records) {
        foreach ($type in @($record.types)) {
            $typesByQualifiedName[$type.qualifiedName] = $type
            if (-not $typesBySimpleName.ContainsKey($type.name)) {
                $typesBySimpleName[$type.name] = [System.Collections.Generic.List[object]]::new()
            }
            $typesBySimpleName[$type.name].Add($type)
        }
        foreach ($method in @($record.methodDefinitions)) {
            if (-not $method.declaringType) { continue }
            if (-not $methodsByType.ContainsKey($method.declaringType)) {
                $methodsByType[$method.declaringType] = [System.Collections.Generic.List[string]]::new()
            }
            $methodsByType[$method.declaringType].Add($method.name)
        }
    }

    foreach ($record in $Records) {
        foreach ($type in @($record.types)) {
            $resolvedBases = [System.Collections.Generic.List[string]]::new()
            foreach ($rawBase in @($type.rawBaseTypes)) {
                foreach ($resolvedBase in (Resolve-CSharpTypeName -RawType $rawBase -Record $record `
                    -TypesBySimpleName $typesBySimpleName -TypesByQualifiedName $typesByQualifiedName)) {
                    $resolvedBases.Add($resolvedBase)
                }
            }
            $type.baseTypes = @($resolvedBases | Sort-Object -Unique)
        }
    }

    $resolvedCallCount = 0
    foreach ($record in $Records) {
        $code = $recordTexts[$record.path]
        $variableTypes = @{}
        $declarationPattern = '(?m)(?<![\w.])((?:global::)?[A-Z_]\w*(?:\.[A-Z_]\w*)*(?:<[^;=(){}]+>)?(?:\[\])?\??)\s+([_A-Za-z]\w*)\s*(?=[=;,\)])'
        foreach ($match in [regex]::Matches($code, $declarationPattern)) {
            $variableTypes[$match.Groups[2].Value] = $match.Groups[1].Value
        }
        $varNewPattern = '\bvar\s+([_A-Za-z]\w*)\s*=\s*new\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:<[^;(){}]+>)?)'
        foreach ($match in [regex]::Matches($code, $varNewPattern)) {
            $variableTypes[$match.Groups[1].Value] = $match.Groups[2].Value
        }

        $qualifiedReferences = [System.Collections.Generic.List[string]]::new()
        foreach ($simpleName in $typesBySimpleName.Keys) {
            if (-not [regex]::IsMatch($code, "\b$([regex]::Escape($simpleName))\b")) { continue }
            foreach ($resolvedType in (Resolve-CSharpTypeName -RawType $simpleName -Record $record `
                -TypesBySimpleName $typesBySimpleName -TypesByQualifiedName $typesByQualifiedName)) {
                $declaredQualifiedNames = @($record.types | ForEach-Object { $_.qualifiedName })
                if ($declaredQualifiedNames -notcontains $resolvedType) { $qualifiedReferences.Add($resolvedType) }
            }
        }
        $record.qualifiedTypeReferences = @($qualifiedReferences | Sort-Object -Unique)

        $resolvedCalls = [System.Collections.Generic.List[object]]::new()
        $receiverCallPattern = '\b([A-Za-z_]\w*)\s*\.\s*([A-Za-z_]\w*)(?:\s*<[^;{}()]+>)?\s*\('
        foreach ($match in [regex]::Matches($code, $receiverCallPattern)) {
            $receiver = $match.Groups[1].Value
            $methodName = $match.Groups[2].Value
            $candidateTypes = @()
            $bindingSource = $null

            if ($variableTypes.ContainsKey($receiver)) {
                $candidateTypes = @(Resolve-CSharpTypeName -RawType $variableTypes[$receiver] -Record $record `
                    -TypesBySimpleName $typesBySimpleName -TypesByQualifiedName $typesByQualifiedName)
                $bindingSource = 'variable-type'
            }
            elseif ($typesBySimpleName.ContainsKey($receiver)) {
                $candidateTypes = @(Resolve-CSharpTypeName -RawType $receiver -Record $record `
                    -TypesBySimpleName $typesBySimpleName -TypesByQualifiedName $typesByQualifiedName)
                $bindingSource = 'static-type'
            }
            elseif (($receiver -eq 'this' -or $receiver -eq 'base') -and @($record.types).Count -eq 1) {
                $candidateTypes = if ($receiver -eq 'this') { @($record.types[0].qualifiedName) } else { @($record.types[0].baseTypes) }
                $bindingSource = $receiver
            }

            if (@($candidateTypes).Count -ne 1) { continue }
            $owner = Find-CSharpMethodOwner -TypeName $(@($candidateTypes)[0]) -MethodName $methodName `
                -TypeByQualifiedName $typesByQualifiedName -MethodsByType $methodsByType -Visited @{}
            if (-not $owner) { continue }

            $resolvedCalls.Add([pscustomobject]@{
                receiver = $receiver
                method = $methodName
                targetType = $owner
                targetQualifiedName = "$owner.$methodName"
                bindingSource = $bindingSource
                confidence = 'resolved'
            })
        }
        $record.resolvedCalls = @($resolvedCalls | Sort-Object targetQualifiedName, receiver -Unique)
        $resolvedCallCount += @($record.resolvedCalls).Count
    }

    return [pscustomobject]@{
        resolvedCallCount = $resolvedCallCount
        qualifiedTypeCount = $typesByQualifiedName.Count
    }
}
