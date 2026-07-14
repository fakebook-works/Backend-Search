# Fakebook SearchService

SearchService lưu chỉ mục tìm kiếm trong PostgreSQL. Service có hai bề mặt API tách biệt:

- REST nội bộ dùng để đồng bộ chỉ mục giữa các service.
- GraphQL chỉ dùng cho truy vấn từ Gateway.

Contract tích hợp chi tiết cho Gateway/SocialGraph nằm trong `SEARCH_FUSION_HANDOFF.md`.

Client không được gọi REST đồng bộ chỉ mục trực tiếp. Gateway là public/TLS edge và kết nối đến SearchService bằng HTTP trong mạng nội bộ.

## Object type

| Giá trị | Loại |
|---:|---|
| `0` | User |
| `1` | Group |
| `2` | Feed post |
| `3` | Group post |
| `4` | Reel |

## REST nội bộ

Mọi endpoint REST bên dưới yêu cầu header `X-Internal-SearchService-Secret`. Giá trị header phải do service gọi hoặc Gateway nội bộ inject; không nhận giá trị do end user gửi lên.

Contract idempotent ưu tiên:

### Upsert index

```http
PUT /internal/search/indexes/{id}
Content-Type: application/json
X-Internal-SearchService-Secret: <internal-secret>

{
  "objectType": "user",
  "text": "Nguyen Van An"
}
```

`objectType` nhận một trong `user`, `group`, `feedPost`, `groupPost`, `reel`. Endpoint trả `201` khi tạo mới và `200` khi cập nhật.

### Xóa index

```http
DELETE /internal/search/indexes/{id}
X-Internal-SearchService-Secret: <internal-secret>
```

Xóa idempotent và trả `204`, kể cả khi index không còn tồn tại.

### Ghi nhận view

```http
POST /internal/search/indexes/{id}/views
X-Internal-SearchService-Secret: <internal-secret>
```

Endpoint trả `204` khi tăng ranking thành công và `404` nếu index không tồn tại. Đây là operation không idempotent; caller retry sẽ tăng thêm lần nữa cho đến khi contract event/idempotency key được bổ sung.

Các endpoint tương thích cũ vẫn còn hoạt động:

| Method | Path | Input |
|---|---|---|
| `POST` | `/api/SearchEngine/add` | Query `id`, `type`, `textContent` |
| `PUT` | `/api/SearchEngine/edit/{id}` | Query `newTextContent` |
| `DELETE` | `/api/SearchEngine/delete/{id}` | Route `id` |

## GraphQL

Endpoint duy nhất là `POST /graphql`. Mọi request phải đi qua Gateway và có `X-Gateway-Secret`. Gateway có thể truyền user đã xác thực qua `X-User-Id`; SearchService không tin header user nếu gateway secret không hợp lệ.

Schema hiện tại chỉ gồm:

```graphql
type Query {
  fastSearch(keyword: String!): [FastSearchResult]!
  searchUsers(keyword: String!, pageNumber: Int! = 1, pageSize: Int! = 20): UserSearchPage!
  searchGroups(keyword: String!, pageNumber: Int! = 1, pageSize: Int! = 20): GroupSearchPage!
  searchFeedPosts(keyword: String!, pageNumber: Int! = 1, pageSize: Int! = 20): FeedPostSearchPage!
  searchGroupPosts(keyword: String!, pageNumber: Int! = 1, pageSize: Int! = 20): GroupPostSearchPage!
  searchReels(keyword: String!, pageNumber: Int! = 1, pageSize: Int! = 20): ReelSearchPage!
}

union FastSearchResult = UserSearchResult | GroupSearchResult
```

`fastSearch` trả tối đa 5 reference thuộc User (`type=0`) hoặc Group (`type=1`) và giữ thứ hạng chung. Slow search được tách thành năm field theo loại. SearchService chỉ trả `referenceId: ID!`; Gateway dùng Fusion để chuyển sang SocialGraph, nơi sở hữu profile, content, relationship và privacy. Item trong page là nullable vì object có thể đã bị xóa hoặc bị SocialGraph từ chối theo quyền xem.

`pageNumber` từ 1 đến 1.000.000, `pageSize` từ 1 đến 100 và offset tối đa là 100.000 candidate. `pageInfo.hasNextPage` phản ánh candidate trong Search trước khi SocialGraph lọc privacy; không phải tổng số kết quả cuối cùng mà viewer được xem.

Ví dụ:

```graphql
query Search($keyword: String!, $page: Int!) {
  fastSearch(keyword: $keyword) {
    __typename
    ... on UserSearchResult { referenceId }
    ... on GroupSearchResult { referenceId }
  }
  searchUsers(keyword: $keyword, pageNumber: $page, pageSize: 20) {
    items { referenceId }
    pageInfo { pageNumber pageSize hasPreviousPage hasNextPage }
  }
}
```

Trong composed schema, Gateway phải đánh dấu `referenceId` là `@shareable @inaccessible`; client sẽ truy vấn các field đã được SocialGraph hydrate thay vì thấy khóa nội bộ này.

Không có GraphQL mutation ghi dữ liệu. Upsert, delete index và ghi nhận view đều là trách nhiệm của REST nội bộ. Ghi nhận view dùng `POST /internal/search/indexes/{id}/views` với `X-Internal-SearchService-Secret`.

## Export schema cho Fusion Gateway

Schema SDL, source extensions và transport settings được lưu tại `schema.graphqls`, `schema-extensions.graphqls` và `schema-settings.json`. Sau khi thay đổi GraphQL contract, export lại bằng:

```powershell
dotnet run -- schema export --output schema.graphqls
```

Sau export, giữ `schema-settings.json` với source name `Search`, `clientName` là `fusion` và URL theo environment trước khi compose lại `gateway.far`.

## Health và OpenAPI

| Endpoint | Ý nghĩa |
|---|---|
| `/health/live` | Process đang hoạt động, không kiểm tra DB |
| `/health/ready` | Readiness có kiểm tra kết nối PostgreSQL |
| `/health` | Alias của readiness |
| `/swagger/v1/swagger.json` | REST OpenAPI document |
| `/swagger` | Swagger UI |

## Cấu hình an toàn

Các giá trị nhạy cảm trong `appsettings.json` được để trống có chủ đích. Khi chạy service phải cấp cấu hình bằng secret manager, biến môi trường hoặc .NET user-secrets:

| .NET configuration key | Environment variable |
|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `InternalSearchService:Secret` | `InternalSearchService__Secret` |
| `Gateway:InternalSharedSecret` | `Gateway__InternalSharedSecret` |

Ví dụ cho local development bằng user-secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<value-from-secret-manager>"
dotnet user-secrets set "InternalSearchService:Secret" "<value-from-secret-manager>"
dotnet user-secrets set "Gateway:InternalSharedSecret" "<value-from-secret-manager>"
dotnet run --launch-profile http
```

Hoặc PowerShell environment variables:

```powershell
$env:ConnectionStrings__DefaultConnection = "<value-from-secret-manager>"
$env:InternalSearchService__Secret = "<value-from-secret-manager>"
$env:Gateway__InternalSharedSecret = "<value-from-secret-manager>"
dotnet run --launch-profile http
```

`InternalSearchService__Secret` phải có ít nhất 32 ký tự. Gateway và SearchService phải lấy shared secret từ cùng secret manager; không commit secret vào repository hoặc bake vào image.

Credential từng xuất hiện trong file hoặc Git history phải được rotate thủ công tại PostgreSQL/Gateway/secret manager. Xóa giá trị khỏi repository không tự vô hiệu hóa credential cũ.

## Database migration

Không chạy migration legacy `20260711151522_InitialCreate`: migration này đã được chặn có chủ đích vì mapping type cũ sai và giả định bảng đã tồn tại. `BackEndSearch.sql` hiện là schema chuẩn cho fresh database. Trước khi tạo migration EF thay thế phải quyết định database hiện tại được rebuild/full replay từ SocialGraph hay cần migrate dữ liệu tại chỗ; giá trị legacy `post` không đủ thông tin để phân biệt FeedPost với GroupPost.

## Container Linux

Dockerfile dùng Linux .NET 8 và chỉ expose HTTP nội bộ ở port `8080`:

```powershell
docker build -t fakebook-search .
docker run --rm -p 5191:8080 `
  -e ConnectionStrings__DefaultConnection `
  -e InternalSearchService__Secret `
  -e Gateway__InternalSharedSecret `
  fakebook-search
```

## Kiểm chứng

```powershell
dotnet build BackEndSearchFakebook.sln --no-restore -m:1
dotnet test tests/BackEndSearchFakebook.Tests/BackEndSearchFakebook.Tests.csproj --no-build --no-restore -m:1
```

Test project khóa GraphQL contract, input validation và secret comparison; integration test PostgreSQL/Fusion liên service vẫn phải chạy trong môi trường có database và các service thật.
