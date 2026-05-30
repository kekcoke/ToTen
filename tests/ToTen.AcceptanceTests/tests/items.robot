*** Settings ***
Resource        ../resources/keywords.resource
Suite Setup     Suite Setup
Suite Teardown  Suite Teardown

*** Test Cases ***
Get Items Returns 200
    ${response}=    Unauthenticated GET    /items
    Should Be Equal As Integers    ${response.status_code}    200

Create Item Returns 201
    ${body}=    Create Dictionary
    ...    name=Robot Test Item
    ...    description=Created by Robot Framework
    ...    categoryId=${EMPTY}
    ${response}=    Authenticated POST    /items    ${body}
    Should Be Equal As Integers    ${response.status_code}    201

Get Nonexistent Item Returns 404
    ${response}=    Unauthenticated GET    /items/00000000-0000-0000-0000-000000000000
    Should Be Equal As Integers    ${response.status_code}    404
