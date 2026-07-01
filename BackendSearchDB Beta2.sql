-- 1. BẢNG OBJECTS (Lưu thực thể & Phân quyền)
CREATE TABLE objects (
    id       BIGINT PRIMARY KEY, -- ID tự động tăng
    type     VARCHAR(50) NOT NULL,    -- Phân loại: 'USER', 'POST', 'GROUP'
    
    -- Dữ liệu hiển thị
    sort_key        INT DEFAULT 0,           -- Điểm nổi tiếng để xếp hạng ưu tiên
);

-- 2. BẢNG TOKENS (Kho từ khóa tự động)
CREATE TABLE tokens (
    id              BIGINT PRIMARY KEY, -- ID 
    token_text      VARCHAR(255) UNIQUE NOT NULL -- Đảm bảo không bao giờ trùng chữ
);

-- 3. BẢNG TRUNG GIAN (Sổ mục lục)
CREATE TABLE token_object (
    token_id        BIGINT REFERENCES tokens(id) ON DELETE CASCADE,
    object_id       BIGINT REFERENCES objects(id) ON DELETE CASCADE,
    
    -- Khóa chính kép
    PRIMARY KEY (token_id, object_id)
);

-- Tạo thêm Index phụ để tăng tốc độ tìm kiếm ngược (Tìm xem Object này có những Token nào)
CREATE INDEX idx_token_object_obj_id ON token_object(object_id);
