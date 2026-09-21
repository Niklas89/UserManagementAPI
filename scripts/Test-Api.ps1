param([string]$BaseUrl = 'http://localhost:5080')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$client = [System.Net.Http.HttpClient]::new()
$createdIds = [System.Collections.Generic.List[int]]::new()
$count = 0
function Request($method, $path, $body, $expected) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method), "$BaseUrl$path")
    if ($null -ne $body) {
        $request.Content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
    }
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if ([int]$response.StatusCode -ne $expected) { throw "$method $path expected $expected, got $([int]$response.StatusCode): $text" }
    $script:count++
    Write-Host "PASS $method $path -> $expected"
    $result = [pscustomobject]@{ Text = $text; Location = [string]$response.Headers.Location }
    $response.Dispose()
    $request.Dispose()
    return $result
}
function Assert($condition, $message) { if (!$condition) { throw $message } }
$token = [guid]::NewGuid().ToString('N')
$body = @{firstName=' Ada ';lastName='Lovelace';email="ada.$token@example.com"} | ConvertTo-Json
try {
    $null = Request GET '/api/users' $null 200
    $created = Request POST '/api/users' $body 201
    $user = $created.Text | ConvertFrom-Json
    $createdIds.Add($user.id)
    Assert ($user.id -gt 0 -and $user.firstName -eq 'Ada') 'Creation or trimming failed.'
    Assert ($created.Location.EndsWith("/api/users/$($user.id)")) 'Location header is incorrect.'
    $path = "/api/users/$($user.id)"
    $found = (Request GET $path $null 200).Text | ConvertFrom-Json
    Assert ($found.email -eq "ada.$token@example.com") 'Retrieved email differs.'
    $list = (Request GET '/api/users' $null 200).Text | ConvertFrom-Json
    Assert (@($list | Where-Object id -eq $user.id).Count -eq 1) 'User missing from list.'
    $null = Request POST '/api/users' $body.ToUpperInvariant() 409
    $otherBody = @{firstName='Grace';lastName='Hopper';email="grace.$token@example.com"} | ConvertTo-Json
    $other = (Request POST '/api/users' $otherBody 201).Text | ConvertFrom-Json
    $createdIds.Add($other.id)
    $null = Request PUT $path $otherBody 409
    $body = @{firstName='Ada';lastName='Byron';email="ada.$token@example.com"} | ConvertTo-Json
    $null = Request PUT $path $body 204
    $updated = (Request GET $path $null 200).Text | ConvertFrom-Json
    Assert ($updated.lastName -eq 'Byron' -and $updated.id -eq $user.id) 'Update was not saved.'
    foreach ($invalid in @('{}', '{"firstName":" ","lastName":"X","email":"a@example.com"}', '{"firstName":"A","lastName":"B","email":"bad"}', 'null', '{broken')) {
        $null = Request POST '/api/users' $invalid 400
    }
    $tooLong = @{firstName=('x' * 101);lastName='B';email='a@example.com'} | ConvertTo-Json
    $null = Request POST '/api/users' $tooLong 400
    $null = Request PUT $path '{}' 400
    foreach ($field in @('firstName', 'lastName')) {
        foreach ($name in @('-', ' -- ', "'", '123', '!!!')) {
            $invalidName = @{firstName='Ada';lastName='Byron';email="ada.$token@example.com"}
            $invalidName[$field] = $name
            foreach ($method in @('POST', 'PUT')) {
                $target = if ($method -eq 'POST') { '/api/users' } else { $path }
                $failure = (Request $method $target ($invalidName | ConvertTo-Json) 400).Text | ConvertFrom-Json
                Assert ($null -ne $failure.errors.$field) "Missing validation error for $field."
            }
        }
    }
    $unchanged = (Request GET $path $null 200).Text | ConvertFrom-Json
    Assert ($unchanged.lastName -eq 'Byron') 'Invalid update changed the record.'
    Assert ($unchanged.firstName -eq 'Ada') 'Invalid update changed the first name.'
    foreach ($validName in @('Anne-Marie', "O'Connor", ([string][char]0x00C9 + 'lodie'), ([string][char]0x674E))) {
        $validBody = @{firstName=$validName;lastName=$validName;email="name.$([guid]::NewGuid().ToString('N'))@example.com"} | ConvertTo-Json
        $validUser = (Request POST '/api/users' $validBody 201).Text | ConvertFrom-Json
        $createdIds.Add($validUser.id)
        $null = Request PUT "/api/users/$($validUser.id)" $validBody 204
    }
    $null = Request GET '/api/users/0' $null 404
    $null = Request PUT '/api/users/0' $body 404
    $null = Request DELETE '/api/users/0' $null 404
    $null = Request DELETE $path $null 204
    $createdIds.Remove($user.id) | Out-Null
    $null = Request GET $path $null 404
    $null = Request DELETE $path $null 404
    Write-Host "All $count HTTP checks passed, plus response-content assertions."
}
finally {
    foreach ($id in $createdIds) { $null = Request DELETE "/api/users/$id" $null 204 }
    $client.Dispose()
}
