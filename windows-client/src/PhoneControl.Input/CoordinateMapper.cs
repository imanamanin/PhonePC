namespace PhoneControl.Input;

public readonly record struct PhonePoint(double X, double Y);

public readonly record struct WindowRect(double Width, double Height);

public readonly record struct PhoneScreen(int Width, int Height, int Rotation);

/// <summary>
/// Maps WPF window coordinates onto the phone framebuffer, preserving aspect ratio (letterbox).
/// </summary>
public sealed class CoordinateMapper
{
    public PhonePoint? TryMap(WindowRect window, PhoneScreen phone, double windowX, double windowY)
    {
        if (window.Width <= 0 || window.Height <= 0 || phone.Width <= 0 || phone.Height <= 0)
        {
            return null;
        }

        var (contentW, contentH) = LogicalPhoneSize(phone);
        var scale = Math.Min(window.Width / contentW, window.Height / contentH);
        var drawnW = contentW * scale;
        var drawnH = contentH * scale;
        var offsetX = (window.Width - drawnW) / 2.0;
        var offsetY = (window.Height - drawnH) / 2.0;

        if (windowX < offsetX || windowY < offsetY || windowX > offsetX + drawnW || windowY > offsetY + drawnH)
        {
            return null;
        }

        var nx = (windowX - offsetX) / drawnW;
        var ny = (windowY - offsetY) / drawnH;

        return Unrotate(phone, nx, ny);
    }

    private static (double Width, double Height) LogicalPhoneSize(PhoneScreen phone)
    {
        var landscape = phone.Rotation is 90 or 270;
        return landscape
            ? (phone.Height, phone.Width)
            : (phone.Width, phone.Height);
    }

    private static PhonePoint Unrotate(PhoneScreen phone, double nx, double ny)
    {
        double px;
        double py;
        switch (phone.Rotation)
        {
            case 90:
                px = ny * phone.Width;
                py = (1.0 - nx) * phone.Height;
                break;
            case 180:
                px = (1.0 - nx) * phone.Width;
                py = (1.0 - ny) * phone.Height;
                break;
            case 270:
                px = (1.0 - ny) * phone.Width;
                py = nx * phone.Height;
                break;
            default:
                px = nx * phone.Width;
                py = ny * phone.Height;
                break;
        }

        px = Math.Clamp(px, 0, phone.Width - 1e-6);
        py = Math.Clamp(py, 0, phone.Height - 1e-6);
        return new PhonePoint(px, py);
    }
}
