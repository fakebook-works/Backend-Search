# Backend-Search
# 🔍 Fakebook Search Infrastructure & Schema

## 🏗️ 1. Bốn Trụ Cột Cốt Lõi Của Hệ Thống

Hệ thống tìm kiếm được xây dựng dựa trên 4 nguyên lý cốt lõi sau:

*   **Tìm kiếm trên Đồ thị Xã hội (Social Graph):** Khác với Google tìm kiếm các tài liệu web tĩnh, hệ thống này tìm kiếm trên một mạng lưới khổng lồ gồm các Đỉnh (người dùng, trang, bài viết) và các Cạnh (bạn bè, lượt thích, bình luận).
*   **Cỗ máy lập chỉ mục "Unicorn" (Inverted Index):** Do khối lượng dữ liệu quá lớn, hệ thống sử dụng kiến trúc Unicorn để index các chuỗi mối quan hệ (ví dụ: `friend:4`) thay vì từ khóa văn bản thuần túy. Để tăng tốc độ, hệ thống dùng một khóa sắp xếp (`sort-key`) đánh giá điểm tĩnh từ trước và chỉ cắt lấy top kết quả quan trọng nhất.
*   **Cập nhật thời gian thực với "Wormhole":** Công nghệ luồng phân phối này liên tục "lắng nghe" dữ liệu từ MySQL và đẩy thẳng bài viết mới vào bộ máy Index chỉ trong vài giây.
*   **Kết hợp AI và Bảo vệ Quyền riêng tư (ACL):**
    *   *Truy xuất ngữ nghĩa (SSR):* Chạy song song với Unicorn, mô hình SSR biến chữ viết thành các vector không gian siêu chiều (ví dụ: tìm "thức uống cà phê Ý" vẫn ra "cappuccino").
    *   *Kiểm soát truy cập (ACL):* Mọi truy vấn phải đi qua hàng rào bảo mật; nếu bài viết được cài đặt "Chỉ mình tôi", hệ thống sẽ tự động gạt bỏ kết quả đó ngay lập tức đối với người tìm kiếm không hợp lệ.

---
