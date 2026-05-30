*** Settings ***
Resource        ../resources/keywords.resource
Suite Setup     Suite Setup
Suite Teardown  Suite Teardown

*** Test Cases ***
Search Listings Returns 200 Without Auth
    ${response}=    Unauthenticated GET    /api/listings/search
    Should Be Equal As Integers    ${response.status_code}    200

Price Filter Returns Results Within Range
    ${response}=    Unauthenticated GET    /api/listings/search?minPrice=10&maxPrice=100
    Should Be Equal As Integers    ${response.status_code}    200
    ${body}=    Set Variable    ${response.json()}
    ${listings}=    Get From Dictionary    ${body}    listings
    FOR    ${listing}    IN    @{listings}
        ${price}=    Get From Dictionary    ${listing}    price
        Should Be True    ${price} >= 10 and ${price} <= 100
    END
