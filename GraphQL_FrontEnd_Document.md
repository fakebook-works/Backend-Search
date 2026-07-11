# Hướng Dẫn Tích Hợp GraphQL API - Backend Search


---

## 1. QUERIES (Dùng để Truy vấn/Lấy kết quả tìm kiếm)

*Lưu ý: Backend đặt tên là `GetFastSearch` và `GetSlowSearch`, nhưng khi Front-End gọi phải dùng `fastSearch` và `slowSearch`.*

### API 5: Tìm kiếm nhanh (Fast Search)
- **Mục đích:** Lấy top 5 `USER` hoặc `GROUP` có độ nổi tiếng (sort_key) cao nhất.
- **Biến truyền vào (Variables):** `keyword` (String)

**Gói tin GraphQL:**
```graphql
query FastSearch($keyword: String!) {
  fastSearch(keyword: $keyword)
}
```
**Dữ liệu truyền lên (Variables):**
```json
{
  "keyword": "nguyen"
}
```

### API 6: Tìm kiếm chậm (Slow Search)
- **Mục đích:** Tìm kiếm vét cạn trên toàn bộ hệ thống (User, Group, Post) không giới hạn kết quả.
- **Biến truyền vào (Variables):** `keyword` (String)

**Gói tin GraphQL:**
```graphql
query SlowSearch($keyword: String!) {
  slowSearch(keyword: $keyword)
}
```
**Dữ liệu truyền lên (Variables):**
```json
{
  "keyword": "bong"
}
```

---

## 2. MUTATIONS (Dùng để Ghi/Cập nhật/Xóa dữ liệu)

### API 1: Thêm mới Object (Sync & Index)
- **Mục đích:** Khi có 1 User/Group/Post mới được tạo ở hệ thống chính, gọi API này để hệ thống Search lưu bản sao và băm từ khóa.
- **Biến truyền vào (Variables):** `id` (Long), `type` (String - "USER", "GROUP", "POST"), `textContent` (String).

**Gói tin GraphQL:**
```graphql
mutation AddObject($id: Long!, $type: String!, $textContent: String!) {
  addObject(id: $id, type: $type, textContent: $textContent)
}
```
**Dữ liệu truyền lên (Variables):**
```json
{
  "id": 201,
  "type": "USER",
  "textContent": "Tran Bao Ngoc"
}
```

### API 2: Sửa nội dung Object
- **Mục đích:** Khi Object thay đổi tên/nội dung, gọi API này để xóa index cũ và băm lại index mới.
- **Biến truyền vào (Variables):** `id` (Long), `newTextContent` (String).

**Gói tin GraphQL:**
```graphql
mutation EditObject($id: Long!, $newTextContent: String!) {
  editObject(id: $id, newTextContent: $newTextContent)
}
```

### API 3: Xóa Object
- **Mục đích:** Khi một Object bị xóa khỏi hệ thống, gọi API này để dọn dẹp dữ liệu tìm kiếm.
- **Biến truyền vào (Variables):** `id` (Long).

**Gói tin GraphQL:**
```graphql
mutation DeleteObject($id: Long!) {
  deleteObject(id: $id)
}
```

### API 4: Ghi nhận tương tác (Record View)
- **Mục đích:** Front-End gọi hàm này khi người dùng click/view vào một đối tượng cụ thể để cộng điểm xếp hạng tìm kiếm (sort_key).
- **Biến truyền vào (Variables):** `id` (Long).

**Gói tin GraphQL:**
```graphql
mutation RecordView($id: Long!) {
  recordView(id: $id)
}
```

---

## 3. SUBSCRIPTIONS (Lắng nghe sự kiện Real-time)

- **Trạng thái:** **KHÔNG SỬ DỤNG**
- **Giải thích:** 
Hệ thống tìm kiếm hiện tại hoạt động theo mô hình Request - Response (hỏi và đáp ngay lập tức). Các hành động đồng bộ dữ liệu (Người 1, 2 làm) chạy ngầm qua Mutations. Người dùng (Client) không cần giữ kết nối WebSocket liên tục để đợi kết quả tìm kiếm tự nhảy số, do đó Backend Search không định nghĩa gói tin Subscriptions. Front-End chỉ cần quan tâm tới Queries và Mutations là đủ.
