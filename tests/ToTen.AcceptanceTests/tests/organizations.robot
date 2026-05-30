*** Settings ***
Resource        ../resources/keywords.resource
Suite Setup     Suite Setup
Suite Teardown  Suite Teardown

*** Test Cases ***
Create Organization Without Auth Returns 401
    ${body}=    Create Dictionary    name=Robot Org    type=Business
    ${response}=    POST On Session    toten    /api/organizations    json=${body}    expected_status=any
    Should Be Equal As Integers    ${response.status_code}    401

Create Organization With Auth Returns 201
    ${body}=    Create Dictionary    name=Acceptance Test Org    type=Household
    ${response}=    Authenticated POST    /api/organizations    ${body}
    Should Be Equal As Integers    ${response.status_code}    201
