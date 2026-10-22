using ResetExplode.Hardware;

internal static class SerialUsbChecks
{
    private static void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        if (!OperatingSystem.IsLinux()) return;
        string root = Path.Combine(Path.GetTempPath(), "reset-serial-usb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            void Write(string path, string value)
            {
                string target = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, value);
            }
            void Link(string path, string target)
            {
                string link = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(link)!);
                Directory.CreateSymbolicLink(link, Path.GetRelativePath(Path.GetDirectoryName(link)!, Path.Combine(root, target)));
            }
            const string hub = "sys/devices/pci/usb1/1-1";
            const string device = hub + "/1-1.2";
            const string iface = device + "/1-1.2:1.0";
            const string port = iface + "/ttyUSB0/tty/ttyUSB0";
            Write(hub + "/uevent", "DEVTYPE=usb_device");
            Write(hub + "/idVendor", "1234");
            Write(hub + "/product", "NÃO USAR HUB");
            Write(device + "/uevent", "DEVTYPE=usb_device\nPRODUCT=1a86/7523/264");
            Write(device + "/idVendor", "1a86");
            Write(device + "/idProduct", "7523");
            Write(device + "/manufacturer", "Fabricante reportado");
            Write(device + "/product", "Conversor USB");
            Write(device + "/serial", "NÃO COLETAR SÉRIE");
            Write(port + "/dev", "188:0");
            Directory.CreateDirectory(Path.Combine(root, "sys/bus/usb-serial/drivers/ch341"));
            Link(iface + "/ttyUSB0/driver", "sys/bus/usb-serial/drivers/ch341");
            Link("sys/class/tty/ttyUSB0", port);
            var collector = new LinuxSerialCollector(new LinuxFiles(root));
            var first = collector.Collect().Ports.Single();
            Check(first.Usb is { VendorId: "1A86", ProductId: "7523", Manufacturer: "Fabricante reportado", Product: "Conversor USB", Driver: "ch341" }, "Identidade/driver USB não foram encontrados.");
            Check(first.Usb.DevicePath == "/" + device && first.Usb.DriverDevicePath == "/" + iface + "/ttyUSB0", "Origem dos atributos foi perdida.");
            Check(!first.HardwareDescription.Contains("SÉRIE"), "Campo de série foi exposto.");

            // Layout CDC ACM: driver na interface, sem nó intermediário usb-serial.
            const string acmDevice = hub + "/1-1.3";
            const string acmInterface = acmDevice + "/1-1.3:1.0";
            Write(acmDevice + "/uevent", "DEVTYPE=usb_device");
            Write(acmDevice + "/idVendor", "2341");
            Write(acmDevice + "/idProduct", "0043");
            Write(acmInterface + "/tty/ttyACM0/dev", "166:0");
            Directory.CreateDirectory(Path.Combine(root, "sys/bus/usb/drivers/cdc_acm"));
            Link(acmInterface + "/driver", "sys/bus/usb/drivers/cdc_acm");
            Link("sys/class/tty/ttyACM0", acmInterface + "/tty/ttyACM0");
            Check(collector.Collect().Ports.Single(p => p.Name == "ttyACM0").Usb?.Driver == "cdc_acm", "Layout CDC ACM não foi identificado.");

            File.Delete(Path.Combine(root, device + "/product"));
            Write(device + "/idVendor", "xyz!");
            Write(device + "/idProduct", "10000");
            var partial = collector.Collect().Ports.Single(p => p.Name == "ttyUSB0");
            Check(partial.Usb is { Product: null, VendorId: null, ProductId: null }, "Dados inválidos ou hub preencheram atributos ausentes.");
            File.Delete(Path.Combine(root, device + "/idVendor"));
            File.Delete(Path.Combine(root, device + "/idProduct"));
            Check(collector.Collect().Ports.Single(p => p.Name == "ttyUSB0").Usb?.DevicePath == "/" + device, "Dispositivo sem IDs foi confundido com hub.");
            Directory.Delete(Path.Combine(root, "sys/bus/usb-serial/drivers/ch341"));
            Check(collector.Collect().Ports.Single(p => p.Name == "ttyUSB0").Usb?.Driver == null, "Link de driver quebrado virou driver disponível.");
            Check(first.Usb.Product == "Conversor USB" && first.Usb.Driver == "ch341", "Snapshot anterior foi modificado.");
            Directory.Delete(Path.Combine(root, device), true);
            Check(collector.Collect().Ports.All(p => p.Name != "ttyUSB0" || p.Usb == null), "Remoção preservou identificação antiga.");
            // Link circular deve terminar como indisponível, sem loop de coleta.
            Link("sys/class/tty/ttyUSB9", "sys/class/tty/ttyUSB9");
            Check(collector.Collect().Ports.All(p => p.Name != "ttyUSB9" || p.Usb == null), "Link circular foi aceito.");
            Console.WriteLine("OK: identidade USB serial, symlinks, layouts USB/ACM, driver, origem, hub, dados inválidos e remoção.");
        }
        finally { Directory.Delete(root, true); }
    }
}
