using System.Globalization;
using System.Text;

namespace BackEndSearchFakebook.Helper
{
    // Hàm Tokenize: tách chuỗi các từ 
    public static class TextHelper 
    {
        public static List<string> Tokenize (string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>(); // Trả về mảng rỗng nếu đầu vào trống
            }
            // 1. Chuyển toàn bộ thành chữ thường (Lowercase)
            // Invariant casing keeps indexed tokens stable across hosts with different cultures.
            string lowerText = text.ToLowerInvariant().Trim();

            // 2. Loại bỏ dấu tiếng Việt
            string noAccentText = RemoveDiacritics(lowerText);

            // Punctuation and symbols delimit terms as users expect. The previous
            // whitespace-only split indexed "hello," as a different token from "hello".
            var tokens = new List<string>();
            var token = new StringBuilder();
            foreach (var character in noAccentText)
            {
                if (char.IsLetterOrDigit(character))
                {
                    token.Append(character);
                    continue;
                }

                FlushToken(token, tokens);
            }

            FlushToken(token, tokens);
            return tokens;
        }

        // Hàm: Thuật toán bóc tách dấu tiếng Việt bằng Unicode
        private static string RemoveDiacritics(string text)
        {
            // Xử lý riêng chữ 'đ' vì bộ mã Unicode chuẩn không coi 'đ' là 'd' có dấu
            text = text.Replace("đ", "d").Replace("Đ", "d");

            // FormD bóc tách chữ "ễ" thành 3 phần: "e" + "^" + "~"
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                // Kiểm tra xem ký tự hiện tại có phải là dấu câu (NonSpacingMark) hay không
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    // Nếu không phải là dấu thì mới giữ lại (giữ chữ "e", vứt "^" và "~")
                    stringBuilder.Append(c);
                }
            }

            // FormC ghép các ký tự rời rạc lại thành chuỗi hoàn chỉnh
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static void FlushToken(StringBuilder token, ICollection<string> tokens)
        {
            if (token.Length == 0)
            {
                return;
            }

            tokens.Add(token.ToString());
            token.Clear();
        }
    }
 }

