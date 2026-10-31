/****************/

// 202418940 

// Le Thanh Mai  

/****************/
using System;
using System.Collections.Generic;

//  ENUM  

// Trạng thái thiết bị
public enum DeviceStatus { Available, Borrowed, UnderMaintenance }

// Mức độ cảnh báo
public enum AlertSeverity { Information, Warning, Critical }

//   EXCEPTION  

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

//   DỮ LIỆU SỰ KIỆN  

// Dữ liệu đi kèm sự kiện đổi trạng thái
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

// Dữ liệu đi kèm sự kiện cảnh báo
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

//   PUBLISHER: THIẾT BỊ  

// Lớp cha: chỉ phát hiện và công bố sự kiện, không biết ai sẽ nhận
public abstract class TeachingDevice
{
    public string Id { get; }
    public string Name { get; }
    public DeviceStatus Status { get; private set; }

    // Hai event: bên ngoài chỉ được += và -=, không tự phát sinh được
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

    // Đổi trạng thái: cập nhật trước, rồi mới phát sinh event
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

    // Hàm phát event (virtual để lớp con có thể mở rộng)
    protected virtual void OnStatusChanged(DeviceStatusChangedEventArgs e)
    {
        StatusChanged?.Invoke(this, e); // ?. : không có ai đăng ký thì bỏ qua
    }

    protected virtual void OnAlertRaised(DeviceAlertEventArgs e)
    {
        AlertRaised?.Invoke(this, e);
    }
}

//   CÁC LỚP THIẾT BỊ  

// Điều khiển máy chiếu
public class ProjectorRemote : TeachingDevice
{
    public int BatteryLevel { get; private set; }

    public ProjectorRemote(string id, string name, int batteryLevel) : base(id, name)
    {
        if (batteryLevel < 0 || batteryLevel > 100)
            throw new InvalidDeviceReadingException(id, batteryLevel);
        BatteryLevel = batteryLevel;
    }

    // Phân vùng pin: 0 = an toàn, 1 = thấp (< 20), 2 = hết pin (= 0)
    private static int LayVung(int pin)
    {
        if (pin == 0) return 2;
        if (pin < 20) return 1;
        return 0;
    }

    public void UpdateBatteryLevel(int newLevel)
    {
        if (newLevel < 0 || newLevel > 100)
            throw new InvalidDeviceReadingException(Id, newLevel);

        int vung_cu = LayVung(BatteryLevel);
        BatteryLevel = newLevel; // cập nhật trước
        int vung_moi = LayVung(newLevel);

        // Chỉ cảnh báo khi pin xấu đi sang vùng nguy hiểm hơn
        if (vung_moi > vung_cu)
        {
            if (vung_moi == 2)
                RaiseAlert("Pin het (0%)", AlertSeverity.Critical);
            else
                RaiseAlert("Pin thap: " + newLevel + "%", AlertSeverity.Warning);
        }
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

        // Chỉ cảnh báo đúng lúc chuyển từ "có phản hồi" sang "không phản hồi"
        if (truoc_do == true && isResponsive == false)
            RaiseAlert("Dieu hoa phong " + AssignedRoom + " khong phan hoi", AlertSeverity.Critical);
    }
}

// Micro không dây
public class WirelessMicrophone : TeachingDevice
{
    public int BatteryLevel { get; private set; }
    public int SignalQuality { get; private set; }

    public WirelessMicrophone(string id, string name, int batteryLevel, int signalQuality) : base(id, name)
    {
        if (batteryLevel < 0 || batteryLevel > 100)
            throw new InvalidDeviceReadingException(id, batteryLevel);
        if (signalQuality < 0 || signalQuality > 100)
            throw new InvalidDeviceReadingException(id, signalQuality);
        BatteryLevel = batteryLevel;
        SignalQuality = signalQuality;
    }

    // Phân vùng: 0 = an toàn, 1 = thấp (< ngưỡng), 2 = bằng 0
    private static int LayVung(int gia_tri, int nguong)
    {
        if (gia_tri == 0) return 2;
        if (gia_tri < nguong) return 1;
        return 0;
    }

    public void UpdateBatteryLevel(int newLevel)
    {
        if (newLevel < 0 || newLevel > 100)
            throw new InvalidDeviceReadingException(Id, newLevel);

        int vung_cu = LayVung(BatteryLevel, 30);
        BatteryLevel = newLevel;
        int vung_moi = LayVung(newLevel, 30);

        if (vung_moi > vung_cu)
        {
            if (vung_moi == 2)
                RaiseAlert("Pin micro het (0%)", AlertSeverity.Critical);
            else
                RaiseAlert("Pin micro thap: " + newLevel + "%", AlertSeverity.Warning);
        }
    }

    public void UpdateSignalQuality(int newQuality)
    {
        if (newQuality < 0 || newQuality > 100)
            throw new InvalidDeviceReadingException(Id, newQuality);

        int vung_cu = LayVung(SignalQuality, 40);
        SignalQuality = newQuality;
        int vung_moi = LayVung(newQuality, 40);

        if (vung_moi > vung_cu)
        {
            if (vung_moi == 2)
                RaiseAlert("Mat tin hieu (0%)", AlertSeverity.Critical);
            else
                RaiseAlert("Tin hieu yeu: " + newQuality + "%", AlertSeverity.Warning);
        }
    }
}

//   SUBSCRIBER  

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

//   DELEGATE VÀ CALLBACK  

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

//   ĐIỀU PHỐI ĐĂNG KÝ  

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

//   CHƯƠNG TRÌNH KIỂM THỬ  
public class Program
{
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

        // Test 1: đăng ký (gọi 2 lần, lần 2 không được đăng ký trùng)
        Console.WriteLine("=== Test 1: Dang ky subscriber ===");
        var dieu_phoi = new DeviceMonitoringCoordinator(
            danh_sach_thiet_bi, dich_vu_thong_bao, nhat_ky, dich_vu_bao_tri);
        dieu_phoi.Subscribe();
        dieu_phoi.Subscribe();
        may_chieu.ChangeStatus(DeviceStatus.Borrowed);
        Console.WriteLine("So dong log (ky vong 1, khong bi ghi doi): " + nhat_ky.Entries.Count);

        // Test 2: đổi sang cùng trạng thái -> không phát event
        Console.WriteLine("\n=== Test 2: Doi cung trang thai ===");
        may_chieu.ChangeStatus(DeviceStatus.Borrowed);
        Console.WriteLine("So dong log (ky vong van la 1): " + nhat_ky.Entries.Count);
        may_chieu.ChangeStatus(DeviceStatus.Available);
        Console.WriteLine("Sau khi tra ve Available, so dong log (ky vong 2): " + nhat_ky.Entries.Count);

        // Test 3: máy chiếu - cảnh báo theo vùng pin
        Console.WriteLine("\n=== Test 3: May chieu ===");
        Console.WriteLine("Pin 50 -> 15 (ky vong Warning):");
        may_chieu.UpdateBatteryLevel(15);
        Console.WriteLine("Pin 15 -> 10 (cung vung, ky vong khong co canh bao):");
        may_chieu.UpdateBatteryLevel(10);
        Console.WriteLine("Pin 10 -> 0 (ky vong Critical):");
        may_chieu.UpdateBatteryLevel(0);
        Console.WriteLine("Pin 0 -> 50 (pin hoi phuc, khong canh bao):");
        may_chieu.UpdateBatteryLevel(50);

        // Test 4: điều hòa - không phản hồi
        Console.WriteLine("\n=== Test 4: Dieu hoa ===");
        Console.WriteLine("Chuyen sang khong phan hoi (ky vong Critical):");
        dieu_hoa.UpdateResponseState(false);
        Console.WriteLine("Van khong phan hoi (ky vong khong lap lai):");
        dieu_hoa.UpdateResponseState(false);

        // Test 5: micro + delegate lọc (chỉ in Critical)
        Console.WriteLine("\n=== Test 5: Micro va FilteredAlertSubscriber ===");
        var chi_critical = new FilteredAlertSubscriber(
            alert => alert.Severity == AlertSeverity.Critical,
            alert => Console.WriteLine($"  >> CRITICAL (loc rieng): {alert.DeviceId} - {alert.Message}"));
        micro.AlertRaised += chi_critical.HandleAlert; // đăng ký thêm mà không sửa lớp thiết bị

        Console.WriteLine("Pin 80 -> 20 (Warning, bo loc se bo qua):");
        micro.UpdateBatteryLevel(20);
        Console.WriteLine("Tin hieu 70 -> 30 (Warning, bo loc se bo qua):");
        micro.UpdateSignalQuality(30);
        Console.WriteLine("Tin hieu 30 -> 0 (Critical, ca 2 noi deu in):");
        micro.UpdateSignalQuality(0);

        micro.AlertRaised -= chi_critical.HandleAlert; // hủy đăng ký

        // Test 6: giá trị không hợp lệ
        Console.WriteLine("\n=== Test 6: Gia tri khong hop le ===");
        try
        {
            micro.UpdateBatteryLevel(150);
        }
        catch (InvalidDeviceReadingException loi)
        {
            Console.WriteLine("Bat duoc loi: thiet bi " + loi.DeviceId + ", gia tri " + loi.InvalidValue);
            Console.WriteLine("Pin micro khong bi doi: " + micro.BatteryLevel);
        }

        // Test 7: hủy đăng ký -> thiết bị phát event nhưng không ai nhận
        Console.WriteLine("\n=== Test 7: Huy dang ky ===");
        int so_log_truoc = nhat_ky.Entries.Count;
        int so_yeu_cau_truoc = dich_vu_bao_tri.Requests.Count;
        dieu_phoi.Dispose(); // Dispose gọi Unsubscribe
        may_chieu.ChangeStatus(DeviceStatus.UnderMaintenance);
        may_chieu.UpdateBatteryLevel(0);
        Console.WriteLine("Log truoc/sau: " + so_log_truoc + "/" + nhat_ky.Entries.Count + " (ky vong bang nhau)");
        Console.WriteLine("Yeu cau bao tri truoc/sau: " + so_yeu_cau_truoc + "/" + dich_vu_bao_tri.Requests.Count + " (ky vong bang nhau)");

        // Test 8: xem kết quả các subscriber đã ghi lại
        Console.WriteLine("\n=== Test 8: Nhat ky doi trang thai ===");
        foreach (string dong in nhat_ky.Entries)
            Console.WriteLine(dong);

        Console.WriteLine("\n=== Test 9: Yeu cau bao tri ===");
        foreach (string yeu_cau in dich_vu_bao_tri.Requests)
            Console.WriteLine(yeu_cau);

    }
}