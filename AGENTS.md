# Search service agent rules

When embedded in the Fakebook workspace, also read the root API security contract.

- Browser search is GraphQL through Gateway, never a new public Search REST endpoint.
- Derive the caller from TrustedGatewayUserAccessor, never from input userId.
- Relationship-scoped search obtains the allowed ID set from signed internal services and
  intersects results server-side.
- Internal indexing/relationship endpoints require signed HMAC requests and Redis nonce
  replay protection.
- Bound keyword length, page size, candidate IDs and query time; parameterize SQL.
- Runtime DB access uses the search-scoped role and startup DDL remains disabled.
- Automatic retry remains limited to safe HTTP methods.
- Do not log raw queries together with identity or any credential/header.

Run dotnet test BackEndSearchFakebook.sln and add untrusted/wrong-scope/boundary tests.
