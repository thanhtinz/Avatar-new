// FantasyWorld.Server/Services/LocalizationService.cs
namespace FantasyWorld.Server.Services;

public interface ILocalizationService
{
    string Get(string key, string lang = "vi", object? args = null);
    (string Vi, string En) GetBoth(string key, object? args = null);
}

public class LocalizationService : ILocalizationService
{
    // Toàn bộ string game — vi và en
    private static readonly Dictionary<string, (string Vi, string En)> _strings = new()
    {
        // Auth
        ["auth.login_success"]       = ("Đăng nhập thành công",                    "Login successful"),
        ["auth.logout_success"]      = ("Đăng xuất thành công",                    "Logged out successfully"),
        ["auth.register_success"]    = ("Đăng ký thành công",                      "Registration successful"),
        ["auth.invalid_credentials"] = ("Tên đăng nhập hoặc mật khẩu không đúng", "Invalid username or password"),
        ["auth.account_banned"]      = ("Tài khoản đã bị khóa. Lý do: {reason}",  "Account banned. Reason: {reason}"),
        ["auth.token_invalid"]       = ("Token không hợp lệ",                      "Invalid token"),
        ["auth.token_expired"]       = ("Phiên đăng nhập đã hết hạn",              "Session expired, please log in again"),
        ["auth.token_missing"]       = ("Vui lòng đăng nhập để tiếp tục",          "Please log in to continue"),
        ["auth.username_taken"]      = ("Tên đăng nhập đã tồn tại",               "Username already exists"),
        ["auth.email_taken"]         = ("Email đã được sử dụng",                   "Email already in use"),

        // Character
        ["character.create_success"] = ("Tạo nhân vật thành công! Chào mừng {name}!", "Character created! Welcome {name}!"),
        ["character.name_taken"]     = ("Tên nhân vật đã tồn tại",                    "Character name already exists"),
        ["character.not_found"]      = ("Không tìm thấy nhân vật",                    "Character not found"),
        ["character.level_up"]       = ("🎉 {name} đã lên cấp {level}!",             "🎉 {name} leveled up to {level}!"),
        ["character.insufficient_gold"] = ("Không đủ vàng. Cần {need}, có {have}",   "Not enough gold. Need {need}, have {have}"),
        ["character.max_level"]      = ("Đã đạt cấp độ tối đa",                       "Maximum level reached"),

        // Inventory
        ["inventory.item_added"]     = ("Đã nhận: {name} x{qty}",        "Received: {name} x{qty}"),
        ["inventory.item_removed"]   = ("Đã dùng: {name} x{qty}",        "Used: {name} x{qty}"),
        ["inventory.full"]           = ("Túi đồ đã đầy!",                "Inventory is full!"),
        ["inventory.item_not_found"] = ("Vật phẩm không tồn tại",        "Item not found"),
        ["inventory.item_equipped"]  = ("Đã trang bị: {name}",           "Equipped: {name}"),

        // Map
        ["map.enter"]               = ("{name} đã bước vào {map}",       "{name} entered {map}"),
        ["map.leave"]               = ("{name} đã rời khỏi {map}",       "{name} left {map}"),
        ["map.level_required"]      = ("Cần đạt cấp {level}",            "Level {level} required"),
        ["map.teleport_success"]    = ("Đã dịch chuyển đến {dest}",      "Teleported to {dest}"),
        ["map.insufficient_gold"]   = ("Không đủ vàng qua cổng dịch chuyển", "Not enough gold for portal"),

        // Chat
        ["chat.muted"]              = ("Bạn đang bị cấm chat",           "You are muted"),
        ["chat.message_too_long"]   = ("Tin nhắn quá dài (tối đa {max})","Message too long (max {max})"),
        ["chat.whisper_not_found"]  = ("Người chơi {name} không online", "Player {name} is offline"),

        // Pet
        ["pet.caught"]              = ("🎉 Đã bắt được {name}! Rarity: {rarity}", "🎉 Caught {name}! Rarity: {rarity}"),
        ["pet.escaped"]             = ("{name} đã thoát thoát!",                   "{name} escaped!"),
        ["pet.leveled_up"]          = ("Thú cưng {name} lên cấp {level}!",         "Pet {name} leveled up to {level}!"),
        ["pet.hungry"]              = ("Thú cưng {name} đang đói!",                "Your pet {name} is hungry!"),
        ["pet.max_pets"]            = ("Đã đạt giới hạn thú cưng",                 "Pet limit reached"),

        // Clan
        ["clan.created"]            = ("Gia tộc {name} đã được thành lập!",  "Clan {name} has been founded!"),
        ["clan.joined"]             = ("Đã gia nhập gia tộc {name}",          "Joined clan {name}"),
        ["clan.left"]               = ("Đã rời khỏi gia tộc",                  "You left the clan"),
        ["clan.kicked"]             = ("Bạn đã bị trục xuất khỏi gia tộc",    "You were kicked from the clan"),
        ["clan.full"]               = ("Gia tộc đã đầy thành viên",            "Clan is full"),
        ["clan.not_member"]         = ("Bạn không phải thành viên gia tộc",    "You are not a clan member"),

        // Shop
        ["shop.buy_success"]        = ("Đã mua {item} x{qty} với giá {price} vàng", "Purchased {item} x{qty} for {price} gold"),
        ["shop.sell_success"]       = ("Đã bán {item} x{qty} được {price} vàng",    "Sold {item} x{qty} for {price} gold"),
        ["shop.out_of_stock"]       = ("Hết hàng",                                   "Out of stock"),

        // Game world
        ["game.day_start"]          = ("☀️ Bình minh ló rạng tại thế giới Fantasy",         "☀️ Dawn breaks over the Fantasy World"),
        ["game.night_start"]        = ("🌙 Màn đêm buông xuống...",                           "🌙 Darkness falls..."),
        ["game.full_moon"]          = ("🌕 Đêm trăng tròn! Các sinh vật huyền bí xuất hiện!","🌕 Full Moon! Mysterious creatures appear!"),
        ["game.season_spring"]      = ("🌸 Mùa Xuân đã đến!",                                 "🌸 Spring has arrived!"),
        ["game.season_summer"]      = ("☀️ Mùa Hạ đã đến!",                                  "☀️ Summer has arrived!"),
        ["game.season_autumn"]      = ("🍂 Mùa Thu đã đến!",                                  "🍂 Autumn has arrived!"),
        ["game.season_winter"]      = ("❄️ Mùa Đông đã đến!",                                 "❄️ Winter has arrived!"),
        ["game.world_event"]        = ("📢 SỰ KIỆN: {name}",                                  "📢 EVENT: {name}"),

        // Errors
        ["error.server_error"]      = ("Lỗi máy chủ, vui lòng thử lại",    "Server error, please try again"),
        ["error.not_found"]         = ("Không tìm thấy",                     "Not found"),
        ["error.forbidden"]         = ("Bạn không có quyền thực hiện",       "Permission denied"),
        ["error.bad_request"]       = ("Yêu cầu không hợp lệ",              "Invalid request"),
        ["error.rate_limit"]        = ("Quá nhiều yêu cầu, vui lòng chờ",   "Too many requests, please wait"),

        // Auction
        ["auction.bid_placed"]      = ("Đã đặt giá thành công",             "Bid placed successfully"),
        ["auction.outbid"]          = ("Bạn đã bị outbid bởi người khác",   "You have been outbid"),
        ["auction.won"]             = ("Bạn đã thắng phiên đấu giá!",        "You won the auction!"),
        ["auction.expired"]         = ("Phiên đấu giá đã kết thúc",          "Auction has ended"),
        ["auction.cancelled"]       = ("Đã hủy phiên đấu giá",               "Auction cancelled"),
        ["auction.min_bid"]         = ("Giá đặt phải cao hơn {min}",         "Bid must be higher than {min}"),

        // Restaurant
        ["restaurant.applied"]      = ("Đơn mở quán đã được gửi! Chờ vote", "Application submitted! Waiting for votes"),
        ["restaurant.approved"]     = ("Quán {name} đã được phê duyệt!",     "Restaurant {name} has been approved!"),
        ["restaurant.rejected"]     = ("Đơn mở quán không đủ phiếu",         "Application did not get enough votes"),
        ["restaurant.voted"]        = ("Đã bỏ phiếu cho {name}",             "Voted for {name}"),
        ["restaurant.order_success"]= ("Đã đặt món thành công!",              "Order placed successfully!"),
        ["party.joined"]            = ("Đã gia nhập tổ đội",                "Joined the party"),
        ["party.left"]              = ("Đã rời khỏi tổ đội",               "You left the party"),
        ["party.disbanded"]         = ("Tổ đội đã giải tán",                "Party disbanded"),
        ["party.full"]              = ("Tổ đội đã đủ người",                "Party is full"),
        ["party.invite_sent"]       = ("Đã gửi lời mời đến {name}",         "Invite sent to {name}"),
        ["party.not_member"]        = ("Bạn không trong tổ đội",            "You are not in a party"),

        // Relationship
        ["rel.propose_sent"]        = ("Đã gửi lời đề nghị quan hệ",        "Relationship request sent"),
        ["rel.already_exists"]      = ("Quan hệ đã tồn tại",               "Relationship already exists"),
        ["rel.limit_reached"]       = ("Đã đạt giới hạn loại quan hệ này", "Relationship limit reached"),
        ["rel.removed"]             = ("Đã hủy quan hệ",                    "Relationship removed"),
    };

    public string Get(string key, string lang = "vi", object? args = null)
    {
        if (!_strings.TryGetValue(key, out var pair))
            return key;

        var text = lang == "en" ? pair.En : pair.Vi;
        return args is null ? text : Interpolate(text, args);
    }

    public (string Vi, string En) GetBoth(string key, object? args = null)
    {
        if (!_strings.TryGetValue(key, out var pair))
            return (key, key);

        if (args is null) return pair;
        return (Interpolate(pair.Vi, args), Interpolate(pair.En, args));
    }

    private static string Interpolate(string template, object args)
    {
        // Dùng reflection để thay {propName} bằng giá trị
        var type = args.GetType();
        var result = template;
        foreach (var prop in type.GetProperties())
        {
            var placeholder = $"{{{prop.Name}}}";
            var value       = prop.GetValue(args)?.ToString() ?? "";
            result          = result.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
