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
        [hashtable]$TypesByQualifiedName,
        [hashtable]$Visited = @{}
    )

    $simpleName = Get-SimpleCSharpTypeName -TypeName $RawType
    if (-not $simpleName) { return @() }
    $visitKey = "$($Record.path)`0$RawType"
    if ($Visited.ContainsKey($visitKey)) { return @() }
    $Visited[$visitKey] = $true

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
                -TypesBySimpleName $TypesBySimpleName -TypesByQualifiedName $TypesByQualifiedName -Visited $Visited
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

function New-CSharpFileRecord {
    param(
        [string]$Text,
        [string]$Path,
        [long]$SourceLength,
        [string]$SourceHash,
        [hashtable]$KeywordSet
    )

    $code = Remove-CSharpTrivia -Text $Text
    $namespaces = @([regex]::Matches($code, '(?m)^\s*namespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $usings = @([regex]::Matches($code, '(?m)^\s*using\s+(?!static\s)([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $aliases = @([regex]::Matches($code, '(?m)^\s*using\s+([A-Za-z_]\w*)\s*=\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*;') |
        ForEach-Object { [pscustomobject]@{ name = $_.Groups[1].Value; target = $_.Groups[2].Value } })

    $namespaceName = @($namespaces | Select-Object -First 1)
    $typePattern = '(?m)^\s*(?:(?:public|internal|protected|private|static|abstract|sealed|partial|readonly|ref|new)\s+)*(class|interface|struct|enum|record)\s+([A-Za-z_]\w*)(?:\s*<[^{}\r\n]+>)?\s*(?::\s*([^\r\n{]+))?\s*\{'
    $parsedTypes = @([regex]::Matches($code, $typePattern) | ForEach-Object {
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
        $containingTypes = @($parsedTypes | Where-Object {
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
    foreach ($type in $parsedTypes) {
        $constructorPattern = '(?m)^\s*(?:(?:public|private|protected|internal|static|extern|unsafe)\s+)+' + [regex]::Escape($type.name) + '\s*\('
        foreach ($match in [regex]::Matches($code, $constructorPattern)) {
            if ($match.Index -le $type.bodyStart -or $match.Index -gt $type.bodyEnd) { continue }
            $methodDefinitions.Add([pscustomobject]@{
                name = $type.name
                declaringType = $type.qualifiedName
                qualifiedName = "$($type.qualifiedName).$($type.name)"
                returnType = $type.qualifiedName
            })
        }
    }

    $calls = @([regex]::Matches($code, '\b([A-Za-z_]\w*)(?:\s*<[^;{}()]+>)?\s*\(') |
        ForEach-Object { $_.Groups[1].Value } |
        Where-Object { -not $KeywordSet.ContainsKey($_) } | Sort-Object -Unique)
    $identifiers = @([regex]::Matches($code, '\b([A-Za-z_]\w*)\b') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $variableDeclarations = [System.Collections.Generic.List[object]]::new()
    $declarationPattern = '(?m)(?<![\w.])((?:global::)?[A-Z_]\w*(?:\.[A-Z_]\w*)*(?:<[^;=(){}]+>)?(?:\[\])?\??)\s+([_A-Za-z]\w*)\s*(?=[=;,\)])'
    foreach ($match in [regex]::Matches($code, $declarationPattern)) {
        $variableDeclarations.Add([pscustomobject]@{ name = $match.Groups[2].Value; rawType = $match.Groups[1].Value })
    }
    $varNewPattern = '\bvar\s+([_A-Za-z]\w*)\s*=\s*new\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:<[^;(){}]+>)?)'
    foreach ($match in [regex]::Matches($code, $varNewPattern)) {
        $variableDeclarations.Add([pscustomobject]@{ name = $match.Groups[1].Value; rawType = $match.Groups[2].Value })
    }
    $receiverCalls = @([regex]::Matches($code, '\b([A-Za-z_]\w*)\s*\.\s*([A-Za-z_]\w*)(?:\s*<[^;{}()]+>)?\s*\(') |
        ForEach-Object { [pscustomobject]@{ receiver = $_.Groups[1].Value; method = $_.Groups[2].Value } } |
        Sort-Object receiver, method -Unique)
    $types = @($parsedTypes | ForEach-Object {
        [pscustomobject]@{
            kind = $_.kind
            name = $_.name
            qualifiedName = $_.qualifiedName
            rawBaseTypes = @($_.rawBaseTypes)
            baseTypes = @()
        }
    })

    return [pscustomobject]@{
        path = $Path
        sourceLength = $SourceLength
        sourceHash = $SourceHash
        namespaces = $namespaces
        usings = $usings
        aliases = $aliases
        types = $types
        methods = @($methodDefinitions | ForEach-Object { $_.name } | Sort-Object -Unique)
        methodDefinitions = @($methodDefinitions)
        calls = $calls
        identifiers = $identifiers
        variableDeclarations = @($variableDeclarations)
        receiverCalls = $receiverCalls
        typeReferences = @()
        qualifiedTypeReferences = @()
        resolvedCalls = @()
    }
}

function Add-CSharpTypeBindings {
    param([System.Collections.IEnumerable]$Records)

    $recordArray = @($Records)
    $typesBySimpleName = @{}
    $typesByQualifiedName = @{}
    $methodsByType = @{}
    foreach ($record in $recordArray) {
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
    Write-CodebaseQueryProgress -Message 'binding symbol tables ready' -Stage 'bind' -Completed 0 -Total $recordArray.Count

    for ($recordIndex = 0; $recordIndex -lt $recordArray.Count; $recordIndex++) {
        $record = $recordArray[$recordIndex]
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
    Write-CodebaseQueryProgress -Message 'binding base types ready' -Stage 'bind' -Completed 0 -Total $recordArray.Count

    $resolvedCallCount = 0
    $methodOwnerCache = @{}
    for ($recordIndex = 0; $recordIndex -lt $recordArray.Count; $recordIndex++) {
        $record = $recordArray[$recordIndex]
        $variableTypes = @{}
        foreach ($declaration in @($record.variableDeclarations)) {
            $variableTypes[$declaration.name] = $declaration.rawType
        }
        $resolvedReceiverTypes = @{}

        $qualifiedReferences = [System.Collections.Generic.List[string]]::new()
        $references = [System.Collections.Generic.List[string]]::new()
        $declaredTypeNames = @($record.types | ForEach-Object { $_.name })
        $declaredQualifiedNames = @($record.types | ForEach-Object { $_.qualifiedName })
        foreach ($simpleName in @($record.identifiers)) {
            if (-not $typesBySimpleName.ContainsKey($simpleName)) { continue }
            if ($declaredTypeNames -notcontains $simpleName) { $references.Add($simpleName) }
            foreach ($resolvedType in (Resolve-CSharpTypeName -RawType $simpleName -Record $record `
                -TypesBySimpleName $typesBySimpleName -TypesByQualifiedName $typesByQualifiedName)) {
                if ($declaredQualifiedNames -notcontains $resolvedType) { $qualifiedReferences.Add($resolvedType) }
            }
        }
        $record.typeReferences = @($references | Sort-Object -Unique)
        $record.qualifiedTypeReferences = @($qualifiedReferences | Sort-Object -Unique)

        $resolvedCalls = [System.Collections.Generic.List[object]]::new()
        foreach ($call in @($record.receiverCalls)) {
            $receiver = $call.receiver
            $methodName = $call.method
            $candidateTypes = @()
            $bindingSource = $null

            if ($variableTypes.ContainsKey($receiver)) {
                if (-not $resolvedReceiverTypes.ContainsKey($receiver)) {
                    $resolvedReceiverTypes[$receiver] = @(Resolve-CSharpTypeName -RawType $variableTypes[$receiver] -Record $record `
                        -TypesBySimpleName $typesBySimpleName -TypesByQualifiedName $typesByQualifiedName)
                }
                $candidateTypes = @($resolvedReceiverTypes[$receiver])
                $bindingSource = 'variable-type'
            }
            elseif ($typesBySimpleName.ContainsKey($receiver)) {
                if (-not $resolvedReceiverTypes.ContainsKey($receiver)) {
                    $resolvedReceiverTypes[$receiver] = @(Resolve-CSharpTypeName -RawType $receiver -Record $record `
                        -TypesBySimpleName $typesBySimpleName -TypesByQualifiedName $typesByQualifiedName)
                }
                $candidateTypes = @($resolvedReceiverTypes[$receiver])
                $bindingSource = 'static-type'
            }
            elseif (($receiver -eq 'this' -or $receiver -eq 'base') -and @($record.types).Count -eq 1) {
                $candidateTypes = if ($receiver -eq 'this') { @($record.types[0].qualifiedName) } else { @($record.types[0].baseTypes) }
                $bindingSource = $receiver
            }

            if (@($candidateTypes).Count -ne 1) { continue }
            $candidateType = $(@($candidateTypes)[0])
            $methodOwnerKey = "$candidateType`0$methodName"
            if (-not $methodOwnerCache.ContainsKey($methodOwnerKey)) {
                $methodOwnerCache[$methodOwnerKey] = Find-CSharpMethodOwner -TypeName $candidateType -MethodName $methodName `
                    -TypeByQualifiedName $typesByQualifiedName -MethodsByType $methodsByType -Visited @{}
            }
            $owner = $methodOwnerCache[$methodOwnerKey]
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
        if (($recordIndex + 1) % 25 -eq 0 -or $recordIndex + 1 -eq $recordArray.Count) {
            Write-CodebaseQueryProgress -Message "binding $($recordIndex + 1)/$($recordArray.Count) files" -Stage 'bind' -Completed ($recordIndex + 1) -Total $recordArray.Count
        }
    }

    return [pscustomobject]@{
        resolvedCallCount = $resolvedCallCount
        qualifiedTypeCount = $typesByQualifiedName.Count
    }
}
