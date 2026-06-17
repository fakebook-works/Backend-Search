-- Tạo Schema cho tính năng Search
CREATE SCHEMA IF NOT EXISTS fb_search;
SET search_path TO fb_search;

-- BẢNG 1: THỰC THỂ TÌM KIẾM (Đại diện cho các kết quả sẽ hiển thị ra)
CREATE TABLE search_entity (
    entity_id       BIGINT PRIMARY KEY, -- ID của kết quả (có thể là ID người dùng, bài viết, trang)
    entity_type     VARCHAR(50) NOT NULL, -- Loại kết quả: 'USER', 'POST', 'GROUP'
    
    -- STATIC RANK: Điểm ưu tiên hiển thị 
    sort_key        INT DEFAULT 0, -- Người nổi tiếng hoặc bài viết nhiều Like sẽ có điểm cao
    
    -- QUYỀN RIÊNG TƯ (ACL)
    privacy_level   INT DEFAULT 2, -- 0: Chỉ mình tôi, 1: Bạn bè, 2: Công khai
    owner_id        BIGINT NOT NULL, -- Ai là chủ sở hữu cái này (Để check quyền)
    
    created_at      TIMESTAMPTZ DEFAULT now()
);

-- BẢNG 2: MỤC LỤC TỪ KHÓA (Inverted Index - Trái tim của hệ thống Unicorn)
CREATE TABLE inverted_index (
    term            VARCHAR(255) NOT NULL, -- Từ khóa (Ví dụ: 'name:nguyen', 'lives_in:hanoi', 'friend:4')
    entity_id       BIGINT REFERENCES search_entity(entity_id) ON DELETE CASCADE,
    
    -- Tạo khóa chính kép để không bị trùng lặp
    PRIMARY KEY (term, entity_id)
);

-- Tạo Index (Chỉ mục) để tăng tốc độ tìm kiếm tuyệt đối
CREATE INDEX idx_term_search ON inverted_index(term);