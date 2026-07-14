# Search Fusion handoff

Tài liệu này là contract bàn giao giữa Search, API Gateway và SocialGraph. Search chỉ sở hữu token, ranking và candidate ID. SocialGraph sở hữu dữ liệu entity, relationship và privacy. Gateway sở hữu authentication boundary, trusted headers và Fusion composition.

## Search source schema hiện đã triển khai

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

type UserSearchResult { referenceId: ID! }
type GroupSearchResult { referenceId: ID! }
type FeedPostSearchResult { referenceId: ID! }
type GroupPostSearchResult { referenceId: ID! }
type ReelSearchResult { referenceId: ID! }

type SearchPageInfo {
  pageNumber: Int!
  pageSize: Int!
  hasPreviousPage: Boolean!
  hasNextPage: Boolean!
}
```

Các `items` đều nullable (`[Type]!`) để lookup có thể trả `null` khi entity stale, deleted hoặc viewer không có quyền. Raw `slowSearch: [Long!]!` đã bị xóa.

## Việc giao cho agent API Gateway

1. Thêm source folder `Gateway/schema/Search` từ `schema.graphqls` và `schema-settings.json` của Search.
2. Giữ source name `Search`, `clientName: fusion`, URL development `http://localhost:5191/graphql`, URL production `http://search:8080/graphql` hoặc sửa hostname đúng deployment thực tế.
3. Thêm source-schema extension sau cho Search và SocialGraph:

```graphql
extend type UserSearchResult { referenceId: ID! @shareable @inaccessible }
extend type GroupSearchResult { referenceId: ID! @shareable @inaccessible }
extend type FeedPostSearchResult { referenceId: ID! @shareable @inaccessible }
extend type GroupPostSearchResult { referenceId: ID! @shareable @inaccessible }
extend type ReelSearchResult { referenceId: ID! @shareable @inaccessible }
```

4. Trong SocialGraph source extension, đánh dấu năm root lookup nullable là `@lookup @internal`:

```graphql
extend type Query {
  userSearchResult(referenceId: ID!): UserSearchResult @lookup @internal
  groupSearchResult(referenceId: ID!): GroupSearchResult @lookup @internal
  feedPostSearchResult(referenceId: ID!): FeedPostSearchResult @lookup @internal
  groupPostSearchResult(referenceId: ID!): GroupPostSearchResult @lookup @internal
  reelSearchResult(referenceId: ID!): ReelSearchResult @lookup @internal
}
```

5. Regenerate `gateway.far`; kiểm tra query plan có transition Search -> SocialGraph.
6. Gateway phải tự thay thế header client gửi vào và forward trusted `X-Gateway-Secret`, `X-User-Id`, `Authorization`, `X-Correlation-ID`. Không forward nguyên `X-User-Id` hoặc `X-Gateway-Secret` từ client.
7. Thêm integration tests cho fast union, năm typed search, stale lookup, denied lookup, nullable item và trusted-header propagation.

## Việc giao cho agent SocialGraph

1. Tạo năm projection type đúng tên: `UserSearchResult`, `GroupSearchResult`, `FeedPostSearchResult`, `GroupPostSearchResult`, `ReelSearchResult`.
2. Tạo lookup/DataLoader theo batch bằng `referenceId`. Viewer lấy từ trusted gateway context, không nhận `viewerId` từ GraphQL argument.
3. Entity deleted, blocked hoặc không đủ quyền phải trả `null`.
4. FeedPost/GroupPost tái sử dụng batch privacy hiện có (`GetPostDetailsAsync(viewerId, ids)`); Reel cần detail loader và privacy rule riêng.
5. Sửa producer gọi Search REST:

   - User: `objectType = "user"`.
   - Group: `objectType = "group"`.
   - Feed post: đổi `"post"` thành `"feedPost"`.
   - Group post: đổi `"post"` thành `"groupPost"`.
   - Reel: bổ sung upsert/delete với `objectType = "reel"`.

6. Upsert dùng `PUT /internal/search/indexes/{id}` và delete dùng `DELETE /internal/search/indexes/{id}` với `X-Internal-SearchService-Secret`.
7. Nên dùng outbox/replay. Contract hiện chưa có `sourceVersion`, nên retry cũ có thể ghi đè dữ liệu mới hoặc làm sống lại index đã xóa.

## Shape mong muốn sau composition

```graphql
type UserSearchResult {
  id: ID!
  avatar: String!
  name: String!
  bio: String!
  isVerified: Boolean!
  privacy: Int!
  relationship: UserSearchRelationship!
}

union UserSearchRelationship = FriendSearchRelationship | FollowSearchRelationship
type FriendSearchRelationship { friendCount: Long!, canFriend: Boolean! }
type FollowSearchRelationship { followerCount: Long!, canFollow: Boolean! }

type GroupSearchResult {
  id: ID!
  avatar: String!
  name: String!
  privacy: Int!
  memberCount: Long!
  bio: String!
  canJoin: Boolean!
}

type FeedPostSearchResult {
  id: ID!
  author: UserSearchResult!
  content: String!
  createdAt: String!
  privacy: Int!
  media: [MediaResult!]!
}

type GroupPostSearchResult {
  id: ID!
  group: GroupSearchResult!
  author: UserSearchResult!
  content: String!
  createdAt: String!
  media: [MediaResult!]!
}

type ReelSearchResult {
  id: ID!
  author: UserSearchResult!
  content: String!
  createdAt: String!
  media: [MediaResult!]!
}
```

GraphQL không thể làm field biến mất theo privacy của từng row. Union `UserSearchRelationship` bảo đảm privacy `0` chỉ trả friend mode và privacy `1` chỉ trả follow mode. Nếu frontend không dùng union, phương án khác là bốn field nullable, nhưng contract yếu hơn.

## Quyết định bắt buộc trước khi chốt privacy

1. User privacy `0` có đúng là friend mode và `1` là follower mode không?
2. `canFriend` false khi self, already-friend, outgoing/incoming pending hoặc blocked? Pending request hiện lưu ở association/type nào?
3. `canFollow` false khi self, already-following hoặc blocked? Có approval/pending follow không?
4. Group private có xuất hiện trong fast/slow search với non-member không?
5. `canJoin` của public/private group xử lý member, admin, pending join request và banned user ra sao?
6. FeedPost privacy có đúng `0=public`, `1=friends` không? Có followers, only-me hoặc custom audience không? Cần truth table cho anonymous, owner, friend và blocked.
7. GroupPost private cho phép owner/member/admin như code hiện tại hay còn role/rule khác?
8. Reel hiện không có privacy riêng: luôn public, theo privacy author hay phải thêm privacy vào Reel? Reel có trả media không?
9. Search có cho anonymous không, hay cả sáu query đều bắt buộc đăng nhập?
10. Giữ field thời gian cũ `create` hay chuẩn hóa `createdAt`; giữ privacy `Int` hay đổi sang enum?

## Quyết định pagination/privacy

Fusion reference hydrate được dữ liệu nhưng không tự loại và lấy bù candidate. Nếu Search lấy 20 ID rồi SocialGraph từ chối 15 ID, page còn 5 item và có thể chứa `null`; `hasNextPage` vẫn dựa trên candidate trước privacy. Vấn đề tương tự xảy ra với fast search hoặc User/Group nếu blocked user/private group phải bị ẩn.

- Phương án đơn giản: chấp nhận sparse/null page như contract hiện tại.
- Phương án khuyến nghị: SocialGraph sở hữu public resolver cho mọi loại cần privacy filtering, gọi một internal candidate endpoint của Search, privacy-check theo batch và scan tiếp cho tới đủ page/top 5. Phương án này cần bổ sung candidate REST ở Search và đổi ownership của các public field liên quan khi compose.

## Quyết định dữ liệu Search

1. Mỗi loại index text nào: User name hay name+bio; Group name hay name+bio; Post/Reel content có gồm hashtag không?
2. Multi-keyword hiện dùng AND: mỗi từ nhập phải prefix-match ít nhất một token đã index. Có muốn đổi sang OR hoặc chỉ prefix từ cuối không?
3. Fast top 5 hiện là top 5 chung giữa User và Group. Có muốn 5 chung hay 5 mỗi loại?
4. Database hiện tại có được drop/recreate và full replay từ SocialGraph không? EF initial migration cũ đang sai mapping và không dựng được fresh DB; không được chạy migration đó.
5. Producer có thể gửi monotonic `sourceVersion`, outbox và full replay không?
