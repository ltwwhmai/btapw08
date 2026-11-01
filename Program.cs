/****************/

// 202418940 

// Le Thanh Mai  

/****************/
using System;
using System.Collections.Generic;

// Enum

// Trạng thái thiết bị
public enum DeviceStatus { Available, Borrowed, UnderMaintenance }

// Mức độ cảnh báo
public enum AlertSeverity { Information, Warning, Critical }

// Vùng giá trị của pin/tín hiệu, xếp theo mức nguy hiểm tăng dần
public enum ReadingZone { Safe, Low, Empty }

// Exception

// Lỗi khi pin hoặc tín hiệu nằm ngoài khoảng 0-100
public class InvalidDeviceReadingException : Exception
{
    public string DeviceId { get; }
    public int InvalidValue { get; }

    public InvalidDeviceReadingException(string deviceId, int invalidValue)
        : base("Gia tri khong hop le: " + invalidValue + " (thiet bi " + deviceId + ")")
    {
        DeviceId = deviceId;
        InvalidValue = invalidValue;
    }
}

// Dữ liệu đi kèm sự kiện

// Dữ liệu sự kiện đổi trạng thái
public class DeviceStatusChangedEventArgs : EventArgs
{
    public string DeviceId { get; }
    public DeviceStatus OldStatus { get; }
    public DeviceStatus NewStatus { get; }
    public DateTime ChangedAt { get; }

    public DeviceStatusChangedEventArgs(
        string deviceId,
        DeviceStatus oldStatus,
        DeviceStatus newStatus,
        DateTime changedAt)
    {
        DeviceId = deviceId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedAt = changedAt;
    }
}

// Dữ liệu sự kiện cảnh báo
public class DeviceAlertEventArgs : EventArgs
{
    public string DeviceId { get; }
    public string Message { get; }
    public AlertSeverity Severity { get; }
    public DateTime OccurredAt { get; }

    public DeviceAlertEventArgs(
        string deviceId,
        string message,
        AlertSeverity severity,
        DateTime occurredAt)
    {
        DeviceId = deviceId;
        Message = message;
        Severity = severity;
        OccurredAt = occurredAt;
    }
}

// Publisher: thiết bị

// Lớp cha: chỉ phát hiện và công bố sự kiện, không biết ai sẽ nhận
public abstract class TeachingDevice
{
    public string Id { get; }
    public string Name { get; }
    public DeviceStatus Status { get; private set; }

    // Bên ngoài chỉ được += và -=, không tự phát sinh event được
    public event EventHandler<DeviceStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<DeviceAlertEventArgs>? AlertRaised;

    protected TeachingDevice(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id khong hop le");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name khong hop le");

        Id = id;
        Name = name;
        Status = DeviceStatus.Available;
    }

    // Đổi trạng thái: cập nhật trước, rồi mới phát event
    public void ChangeStatus(DeviceStatus newStatus)
    {
        if (newStatus == Status) return; // giống trạng thái cũ thì không làm gì

        DeviceStatus oldStatus = Status;
        Status = newStatus;
        OnStatusChanged(new DeviceStatusChangedEventArgs(Id, oldStatus, newStatus, DateTime.Now));
    }

    // Lớp con gọi hàm này để phát cảnh báo
    protected void RaiseAlert(string message, AlertSeverity severity)
    {
        OnAlertRaised(new DeviceAlertEventArgs(Id, message, severity, DateTime.Now));
    }

    protected virtual void OnStatusChanged(DeviceStatusChangedEventArgs e)
    {
        GoiAnToan(StatusChanged, this, e);
    }

    protected virtual void OnAlertRaised(DeviceAlertEventArgs e)
    {
        GoiAnToan(AlertRaised, this, e);
    }

    // Gọi từng handler riêng, một handler lỗi không làm các handler sau bị bỏ qua.
    // Lỗi được gom lại và ném AggregateException sau khi gọi hết handler
    // (thiết bị không in ra màn hình, việc xử lý lỗi để cho bên gọi quyết định)
    private static void GoiAnToan<TArgs>(EventHandler<TArgs>? su_kien, object nguoi_gui, TArgs e)
        where TArgs : EventArgs
    {
        if (su_kien == null) return; // chưa ai đăng ký

        List<Exception>? cac_loi = null;
        foreach (Delegate ham in su_kien.GetInvocationList())
        {
            try
            {
                ((EventHandler<TArgs>)ham)(nguoi_gui, e);
            }
            catch (Exception loi)
            {
                if (cac_loi == null) cac_loi = new List<Exception>();
                cac_loi.Add(loi); // chỉ ghi nhận lỗi, chưa ném ngay
            }
        }

        if (cac_loi != null) throw new AggregateException(cac_loi);
    }

    // Các hàm dùng chung cho pin/tín hiệu (tránh viết lặp ở lớp con)

    // Giá trị phải nằm trong 0-100
    protected void ValidateReading(int gia_tri)
    {
        if (gia_tri < 0 || gia_tri > 100)
            throw new InvalidDeviceReadingException(Id, gia_tri);
    }

    // Xác định vùng: 0 là Empty, dưới ngưỡng là Low, còn lại là Safe
    private static ReadingZone LayVung(int gia_tri, int nguong)
    {
        if (gia_tri == 0) return ReadingZone.Empty;
        if (gia_tri < nguong) return ReadingZone.Low;
        return ReadingZone.Safe;
    }

    // Chỉ cảnh báo khi giá trị xấu đi sang vùng nguy hiểm hơn
    protected void AlertIfWorse(int gia_tri_cu, int gia_tri_moi, int nguong, string ten)
    {
        ReadingZone vung_cu = LayVung(gia_tri_cu, nguong);
        ReadingZone vung_moi = LayVung(gia_tri_moi, nguong);
        if (vung_moi <= vung_cu) return;

        if (vung_moi == ReadingZone.Empty)
            RaiseAlert(ten + " het (0%)", AlertSeverity.Critical);
        else
            RaiseAlert(ten + " thap: " + gia_tri_moi + "%", AlertSeverity.Warning);
    }
}

// Các lớp thiết bị

// Điều khiển máy chiếu
public class ProjectorRemote : TeachingDevice
{
    private const int nguong_pin_thap = 20;

    public int BatteryLevel { get; private set; }

    public ProjectorRemote(string id, string name, int batteryLevel) : base(id, name)
    {
        ValidateReading(batteryLevel);
        BatteryLevel = batteryLevel;
    }

    public void UpdateBatteryLevel(int newLevel)
    {
        ValidateReading(newLevel);
        int pin_cu = BatteryLevel;
        BatteryLevel = newLevel; // cập nhật trước
        AlertIfWorse(pin_cu, newLevel, nguong_pin_thap, "Pin");
    }
}

// Điều khiển điều hòa
public class AirConditionerRemote : TeachingDevice
{
    public string AssignedRoom { get; }
    public bool IsResponsive { get; private set; }

    public AirConditionerRemote(string id, string name, string assignedRoom) : base(id, name)
    {
        if (string.IsNullOrWhiteSpace(assignedRoom)) throw new ArgumentException("Phong khong hop le");
        AssignedRoom = assignedRoom;
        IsResponsive = true; // ban đầu coi như có phản hồi
    }

    public void UpdateResponseState(bool isResponsive)
    {
        bool truoc_do = IsResponsive;
        IsResponsive = isResponsive;

        // Chỉ cảnh báo đúng lúc chuyển từ có phản hồi sang không phản hồi
        if (truoc_do && !isResponsive)
            RaiseAlert("Dieu hoa phong " + AssignedRoom + " khong phan hoi", AlertSeverity.Critical);
    }
}

// Micro không dây
public class WirelessMicrophone : TeachingDevice
{
    private const int nguong_pin_thap = 30;
    private const int nguong_tin_hieu_thap = 40;

    public int BatteryLevel { get; private set; }
    public int SignalQuality { get; private set; }

    public WirelessMicrophone(string id, string name, int batteryLevel, int signalQuality) : base(id, name)
    {
        ValidateReading(batteryLevel);
        ValidateReading(signalQuality);
        BatteryLevel = batteryLevel;
        SignalQuality = signalQuality;
    }

    public void UpdateBatteryLevel(int newLevel)
    {
        ValidateReading(newLevel);
        int pin_cu = BatteryLevel;
        BatteryLevel = newLevel;
        AlertIfWorse(pin_cu, newLevel, nguong_pin_thap, "Pin micro");
    }

    public void UpdateSignalQuality(int newQuality)
    {
        ValidateReading(newQuality);
        int tin_hieu_cu = SignalQuality;
        SignalQuality = newQuality;
        AlertIfWorse(tin_hieu_cu, newQuality, nguong_tin_hieu_thap, "Tin hieu");
    }
}

// Subscriber

// Hiển thị cảnh báo ra màn hình
public class ConsoleNotificationService
{
    public void HandleAlert(object? sender, DeviceAlertEventArgs e)
    {
        Console.WriteLine($"[{e.Severity}] {e.DeviceId}: {e.Message}");
    }
}

// Ghi lịch sử đổi trạng thái
public class DeviceAuditLogger
{
    private readonly List<string> entries = new List<string>();
    public IReadOnlyList<string> Entries => entries.AsReadOnly();

    public void HandleStatusChanged(object? sender, DeviceStatusChangedEventArgs e)
    {
        entries.Add($"{e.ChangedAt}: {e.DeviceId}, {e.OldStatus} -> {e.NewStatus}");
    }
}

// Tạo yêu cầu bảo trì khi có cảnh báo Critical
public class MaintenanceService
{
    private readonly List<string> requests = new List<string>();
    public IReadOnlyList<string> Requests => requests.AsReadOnly();

    public void HandleCriticalAlert(object? sender, DeviceAlertEventArgs e)
    {
        if (e.Severity != AlertSeverity.Critical) return; // chỉ quan tâm Critical

        requests.Add($"Maintenance request for {e.DeviceId}: {e.Message}");
    }
}

// Delegate và callback

// Delegate tự định nghĩa: nhận cảnh báo, trả về true nếu muốn xử lý
public delegate bool AlertFilter(DeviceAlertEventArgs alert);

// Subscriber chỉ xử lý những cảnh báo qua được bộ lọc
public class FilteredAlertSubscriber
{
    private readonly AlertFilter filter;
    private readonly Action<DeviceAlertEventArgs> action;

    public FilteredAlertSubscriber(AlertFilter filter, Action<DeviceAlertEventArgs> action)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));
        if (action == null) throw new ArgumentNullException(nameof(action));
        this.filter = filter;
        this.action = action;
    }

    public void HandleAlert(object? sender, DeviceAlertEventArgs e)
    {
        if (filter(e))   // gọi delegate lọc
        {
            action(e);   // gọi callback xử lý
        }
    }
}

// Điều phối đăng ký

// Gom toàn bộ += và -= vào một chỗ
public class DeviceMonitoringCoordinator : IDisposable
{
    private readonly List<TeachingDevice> devices;
    private readonly ConsoleNotificationService notificationService;
    private readonly DeviceAuditLogger auditLogger;
    private readonly MaintenanceService maintenanceService;
    private bool da_dang_ky = false; // tránh đăng ký trùng

    public DeviceMonitoringCoordinator(
        IEnumerable<TeachingDevice> devices,
        ConsoleNotificationService notificationService,
        DeviceAuditLogger auditLogger,
        MaintenanceService maintenanceService)
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        if (notificationService == null) throw new ArgumentNullException(nameof(notificationService));
        if (auditLogger == null) throw new ArgumentNullException(nameof(auditLogger));
        if (maintenanceService == null) throw new ArgumentNullException(nameof(maintenanceService));

        this.devices = new List<TeachingDevice>(devices);
        this.notificationService = notificationService;
        this.auditLogger = auditLogger;
        this.maintenanceService = maintenanceService;
    }

    public void Subscribe()
    {
        if (da_dang_ky) return; // đã đăng ký rồi thì thôi

        foreach (TeachingDevice thiet_bi in devices)
        {
            thiet_bi.StatusChanged += auditLogger.HandleStatusChanged;
            thiet_bi.AlertRaised += notificationService.HandleAlert;
            thiet_bi.AlertRaised += maintenanceService.HandleCriticalAlert;
        }
        da_dang_ky = true;
    }

    public void Unsubscribe()
    {
        if (!da_dang_ky) return; // chưa đăng ký thì không có gì để hủy

        foreach (TeachingDevice thiet_bi in devices)
        {
            thiet_bi.StatusChanged -= auditLogger.HandleStatusChanged;
            thiet_bi.AlertRaised -= notificationService.HandleAlert;
            thiet_bi.AlertRaised -= maintenanceService.HandleCriticalAlert;
        }
        da_dang_ky = false;
    }

    public void Dispose()
    {
        Unsubscribe();
    }
}

// Chương trình kiểm thử
public class Program
{
    // In PASS nếu điều kiện đúng, FAIL nếu sai
    private static void Check(bool dieu_kien, string mo_ta)
    {
        Console.WriteLine((dieu_kien ? "  PASS: " : "  FAIL: ") + mo_ta);
    }

    public static void Main()
    {
        // Tạo 3 thiết bị (publisher)
        var may_chieu = new ProjectorRemote("P01", "Remote may chieu", 50);
        var dieu_hoa = new AirConditionerRemote("AC01", "Remote dieu hoa", "A203");
        var micro = new WirelessMicrophone("M01", "Micro 1", 80, 70);
        var danh_sach_thiet_bi = new List<TeachingDevice> { may_chieu, dieu_hoa, micro };

        // Tạo 3 subscriber
        var dich_vu_thong_bao = new ConsoleNotificationService();
        var nhat_ky = new DeviceAuditLogger();
        var dich_vu_bao_tri = new MaintenanceService();

        // Biến đếm số cảnh báo của từng thiết bị (đăng ký thêm bằng lambda)
        int dem_may_chieu = 0;
        int dem_dieu_hoa = 0;
        may_chieu.AlertRaised += (s, e) => dem_may_chieu++;
        dieu_hoa.AlertRaised += (s, e) => dem_dieu_hoa++;

        // Test 1: đăng ký 2 lần, lần 2 không được đăng ký trùng
        Console.WriteLine("Test 1: Dang ky subscriber");
        var dieu_phoi = new DeviceMonitoringCoordinator(
            danh_sach_thiet_bi, dich_vu_thong_bao, nhat_ky, dich_vu_bao_tri);
        dieu_phoi.Subscribe();
        dieu_phoi.Subscribe();
        may_chieu.ChangeStatus(DeviceStatus.Borrowed);
        Check(nhat_ky.Entries.Count == 1, "chi ghi 1 dong log, khong bi ghi doi");

        // Test 2: đổi sang cùng trạng thái thì không phát event
        Console.WriteLine("\nTest 2: Doi cung trang thai");
        may_chieu.ChangeStatus(DeviceStatus.Borrowed);
        Check(nhat_ky.Entries.Count == 1, "doi cung trang thai, log khong tang");
        may_chieu.ChangeStatus(DeviceStatus.Available);
        Check(nhat_ky.Entries.Count == 2, "doi trang thai khac, log tang len 2");

        // Test 3: máy chiếu, cảnh báo theo vùng pin
        Console.WriteLine("\nTest 3: May chieu");
        may_chieu.UpdateBatteryLevel(15);
        Check(dem_may_chieu == 1, "pin 50 -> 15: co 1 canh bao Warning");
        may_chieu.UpdateBatteryLevel(10);
        Check(dem_may_chieu == 1, "pin 15 -> 10: cung vung, khong canh bao them");
        may_chieu.UpdateBatteryLevel(0);
        Check(dem_may_chieu == 2, "pin 10 -> 0: co them 1 canh bao Critical");
        may_chieu.UpdateBatteryLevel(50);
        Check(dem_may_chieu == 2, "pin 0 -> 50: pin hoi phuc, khong canh bao");

        // Test 4: điều hòa không phản hồi
        Console.WriteLine("\nTest 4: Dieu hoa");
        dieu_hoa.UpdateResponseState(false);
        Check(dem_dieu_hoa == 1, "chuyen sang khong phan hoi: co 1 canh bao Critical");
        dieu_hoa.UpdateResponseState(false);
        Check(dem_dieu_hoa == 1, "van khong phan hoi: khong canh bao lap lai");

        // Test 5: micro và delegate lọc (chỉ xử lý Critical)
        Console.WriteLine("\nTest 5: Micro va FilteredAlertSubscriber");
        int dem_critical_loc = 0;
        var chi_critical = new FilteredAlertSubscriber(
            alert => alert.Severity == AlertSeverity.Critical,
            alert => dem_critical_loc++);
        micro.AlertRaised += chi_critical.HandleAlert; // đăng ký thêm mà không sửa lớp thiết bị

        micro.UpdateBatteryLevel(20);
        Check(dem_critical_loc == 0, "pin 80 -> 20: Warning, bo loc bo qua");
        micro.UpdateSignalQuality(30);
        Check(dem_critical_loc == 0, "tin hieu 70 -> 30: Warning, bo loc bo qua");
        micro.UpdateSignalQuality(0);
        Check(dem_critical_loc == 1, "tin hieu 30 -> 0: Critical, bo loc nhan duoc");
        micro.AlertRaised -= chi_critical.HandleAlert; // hủy đăng ký

        // Test 6: giá trị không hợp lệ
        Console.WriteLine("\nTest 6: Gia tri khong hop le");
        bool bat_duoc_loi_cap_nhat = false;
        try
        {
            micro.UpdateBatteryLevel(150);
        }
        catch (InvalidDeviceReadingException loi)
        {
            bat_duoc_loi_cap_nhat = (loi.DeviceId == "M01" && loi.InvalidValue == 150);
        }
        Check(bat_duoc_loi_cap_nhat, "UpdateBatteryLevel(150) nem InvalidDeviceReadingException");
        Check(micro.BatteryLevel == 20, "pin micro khong bi doi sau khi bi loi");

        bool bat_duoc_loi_khoi_tao = false;
        try
        {
            var may_chieu_loi = new ProjectorRemote("P99", "Loi", -5);
        }
        catch (InvalidDeviceReadingException)
        {
            bat_duoc_loi_khoi_tao = true;
        }
        Check(bat_duoc_loi_khoi_tao, "tao may chieu voi pin -5 nem InvalidDeviceReadingException");

        // Test 7: một handler ném lỗi không được chặn các handler còn lại
        Console.WriteLine("\nTest 7: Handler nem exception");
        var may_chieu_2 = new ProjectorRemote("P02", "Remote may chieu 2", 50);
        may_chieu_2.AlertRaised += (s, e) => throw new Exception("co tinh gay loi");
        may_chieu_2.AlertRaised += dich_vu_bao_tri.HandleCriticalAlert;
        int so_yeu_cau_truoc = dich_vu_bao_tri.Requests.Count;
        bool bat_duoc_aggregate = false;
        try
        {
            may_chieu_2.UpdateBatteryLevel(0);
        }
        catch (AggregateException)
        {
            bat_duoc_aggregate = true; // handler lỗi được báo lại cho bên gọi
        }
        Check(bat_duoc_aggregate, "loi cua handler duoc bao lai bang AggregateException");
        Check(dich_vu_bao_tri.Requests.Count == so_yeu_cau_truoc + 1,
            "MaintenanceService van nhan duoc canh bao du handler truoc do bi loi");
        Check(may_chieu_2.BatteryLevel == 0, "pin van duoc cap nhat du co handler loi");

        // Test 8: nhiều subscriber cho cùng một event đều nhận được
        Console.WriteLine("\nTest 8: Nhieu subscriber cung nhan");
        var may_chieu_3 = new ProjectorRemote("P03", "Remote may chieu 3", 50);
        int dem_a = 0;
        int dem_b = 0;
        may_chieu_3.AlertRaised += (s, e) => dem_a++;
        may_chieu_3.AlertRaised += (s, e) => dem_b++;
        may_chieu_3.UpdateBatteryLevel(10);
        Check(dem_a == 1 && dem_b == 1, "ca 2 subscriber deu nhan duoc canh bao");

        // Test 9: hủy đăng ký thì không ai nhận nữa
        Console.WriteLine("\nTest 9: Huy dang ky");
        int so_log_truoc = nhat_ky.Entries.Count;
        int so_yeu_cau_truoc_2 = dich_vu_bao_tri.Requests.Count;
        dieu_phoi.Dispose(); // Dispose gọi Unsubscribe
        may_chieu.ChangeStatus(DeviceStatus.UnderMaintenance);
        may_chieu.UpdateBatteryLevel(0);
        Check(nhat_ky.Entries.Count == so_log_truoc, "sau khi huy, log khong tang");
        Check(dich_vu_bao_tri.Requests.Count == so_yeu_cau_truoc_2, "sau khi huy, khong co yeu cau bao tri moi");

        // Xem kết quả các subscriber đã ghi lại
        Console.WriteLine("\nNhat ky doi trang thai:");
        foreach (string dong in nhat_ky.Entries)
            Console.WriteLine(dong);

        Console.WriteLine("\nYeu cau bao tri:");
        foreach (string yeu_cau in dich_vu_bao_tri.Requests)
            Console.WriteLine(yeu_cau);

    }
}
