-- 1. BẢNG OBJECTS (Lưu thực thể & Phân quyền)
CREATE TABLE objects (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, -- ID tự động tăng
    entity_type     VARCHAR(50) NOT NULL,    -- Phân loại: 'USER', 'POST', 'GROUP'
    
    -- Dữ liệu hiển thị
    content         TEXT NOT NULL,           -- Nội dung (Tên người/Tên nhóm/Nội dung bài)
    sort_key        INT DEFAULT 0,           -- Điểm nổi tiếng để xếp hạng ưu tiên
    
    -- Hàng rào bảo mật (Quan trọng)
    owner_id        BIGINT NOT NULL,         -- ID của User tạo ra Object này
    privacy_level   INT DEFAULT 2,           -- 0: Private, 1: Friends, 2: Public
    
    -- Thời gian
    created_at      TIMESTAMPTZ DEFAULT now()
);

-- 2. BẢNG TOKENS (Kho từ khóa tự động)
CREATE TABLE tokens (
    id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY, -- ID tự động tăng
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
