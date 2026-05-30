*** Settings ***
Library     RequestsLibrary

*** Variables ***
${BASE_URL}     http://localhost:5000
${API_KEY}      ${EMPTY}

*** Keywords ***
Suite Setup
    Create Session    toten    ${BASE_URL}    verify=False

Suite Teardown
    Delete All Sessions

Authenticated POST
    [Arguments]    ${path}    ${body}
    ${headers}=    Create Dictionary    Content-Type=application/json    Authorization=Bearer ${API_KEY}
    ${response}=    POST On Session    toten    ${path}    json=${body}    headers=${headers}    expected_status=any
    RETURN    ${response}

Authenticated GET
    [Arguments]    ${path}
    ${headers}=    Create Dictionary    Authorization=Bearer ${API_KEY}
    ${response}=    GET On Session    toten    ${path}    headers=${headers}    expected_status=any
    RETURN    ${response}

Unauthenticated GET
    [Arguments]    ${path}
    ${response}=    GET On Session    toten    ${path}    expected_status=any
    RETURN    ${response}
