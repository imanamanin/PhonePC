using System.Runtime.InteropServices;

#nullable disable

namespace PhoneControl.Desktop.Decoding;

internal static class MfGuids
{
    public static readonly Guid Transform = new("bf94c121-5b05-4e7c-bc22-201fd6396b14");
    public static readonly Guid H264Decoder = new("62CE7E72-4C71-4D20-B15D-452407E58D52");
    public static readonly Guid H265Decoder = new("420A51A3-D605-430C-B4FC-45274FA6C562");
    public static readonly Guid Video = new("73646976-0000-0010-8000-00AA00389B71");
    public static readonly Guid H264 = new("34363248-0000-0010-8000-00AA00389B71");
    public static readonly Guid Hevc = new("43564548-0000-0010-8000-00AA00389B71");
    public static readonly Guid Nv12 = new("3231564E-0000-0010-8000-00AA00389B71");
    public static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    public static readonly Guid InterlaceMode = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    public static readonly Guid DefaultStride = new("644b4e48-1e7b-4541-9eeb-d8678c3f1f9d");
}

[StructLayout(LayoutKind.Sequential)]
internal struct MftInputStreamInfo
{
    public long MaxLatency;
    public int Flags;
    public int Size;
    public int MaxLookahead;
    public int Alignment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MftOutputStreamInfo
{
    public int Flags;
    public int Size;
    public int Alignment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MftOutputDataBuffer
{
    public int StreamId;
    public IntPtr Sample;
    public int Status;
    public IntPtr Events;
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3")]
internal interface IMFAttributes
{
    [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
    [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
    [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out int pbResult);
    [PreserveSig] int Compare(IMFAttributes pTheirs, int matchType, out int pbResult);
    [PreserveSig] int GetUINT32(ref Guid guidKey, out int punValue);
    [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
    [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
    [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
    [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
    [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, int cchBufSize, IntPtr pcchLength);
    [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
    [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
    [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize, IntPtr pcbBlobSize);
    [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
    [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
    [PreserveSig] int DeleteItem(ref Guid guidKey);
    [PreserveSig] int DeleteAllItems();
    [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
    [PreserveSig] int SetUINT64(ref Guid guidKey, ulong unValue);
    [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
    [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
    [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize);
    [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    [PreserveSig] int LockStore();
    [PreserveSig] int UnlockStore();
    [PreserveSig] int GetCount(out int pcItems);
    [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
    [PreserveSig] int CopyAllItems(IMFAttributes pDest);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555")]
internal interface IMFMediaType
{
    [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
    [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
    [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out int pbResult);
    [PreserveSig] int Compare(IMFAttributes pTheirs, int matchType, out int pbResult);
    [PreserveSig] int GetUINT32(ref Guid guidKey, out int punValue);
    [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
    [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
    [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
    [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
    [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, int cchBufSize, IntPtr pcchLength);
    [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
    [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
    [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize, IntPtr pcbBlobSize);
    [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
    [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
    [PreserveSig] int DeleteItem(ref Guid guidKey);
    [PreserveSig] int DeleteAllItems();
    [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
    [PreserveSig] int SetUINT64(ref Guid guidKey, ulong unValue);
    [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
    [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
    [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize);
    [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    [PreserveSig] int LockStore();
    [PreserveSig] int UnlockStore();
    [PreserveSig] int GetCount(out int pcItems);
    [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
    [PreserveSig] int CopyAllItems(IMFAttributes pDest);
    [PreserveSig] int GetMajorType(out Guid pguidMajorType);
    [PreserveSig] int IsCompressedFormat(out int pfCompressed);
    [PreserveSig] int IsEqual(IMFMediaType pIMediaType, out int pdwFlags);
    [PreserveSig] int GetRepresentation(Guid guidRepresentation, out IntPtr ppvRepresentation);
    [PreserveSig] int FreeRepresentation(Guid guidRepresentation, IntPtr pvRepresentation);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("045fa326-e56a-40bb-bdde-2f3c2c1a7a3c")]
internal interface IMFMediaBuffer
{
    [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
    [PreserveSig] int Unlock();
    [PreserveSig] int GetCurrentLength(out int pcbCurrentLength);
    [PreserveSig] int SetCurrentLength(int cbCurrentLength);
    [PreserveSig] int GetMaxLength(out int pcbMaxLength);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("c40a00f4-b39b-44ce-8d2d-b5d2d8e1e4b5")]
internal interface IMFSample
{
    [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
    [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
    [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out int pbResult);
    [PreserveSig] int Compare(IMFAttributes pTheirs, int matchType, out int pbResult);
    [PreserveSig] int GetUINT32(ref Guid guidKey, out int punValue);
    [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
    [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
    [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
    [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
    [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, int cchBufSize, IntPtr pcchLength);
    [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
    [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
    [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize, IntPtr pcbBlobSize);
    [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
    [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
    [PreserveSig] int DeleteItem(ref Guid guidKey);
    [PreserveSig] int DeleteAllItems();
    [PreserveSig] int SetUINT32(ref Guid guidKey, int unValue);
    [PreserveSig] int SetUINT64(ref Guid guidKey, ulong unValue);
    [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
    [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
    [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, int cbBufSize);
    [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    [PreserveSig] int LockStore();
    [PreserveSig] int UnlockStore();
    [PreserveSig] int GetCount(out int pcItems);
    [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
    [PreserveSig] int CopyAllItems(IMFAttributes pDest);
    [PreserveSig] int GetSampleFlags(out int pdwSampleFlags);
    [PreserveSig] int SetSampleFlags(int dwSampleFlags);
    [PreserveSig] int GetSampleTime(out long phnsSampleTime);
    [PreserveSig] int SetSampleTime(long hnsSampleTime);
    [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
    [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
    [PreserveSig] int GetBufferCount(out int pdwBufferCount);
    [PreserveSig] int GetBufferByIndex(int dwIndex, out IMFMediaBuffer ppBuffer);
    [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
    [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
    [PreserveSig] int RemoveBufferByIndex(int dwIndex);
    [PreserveSig] int RemoveAllBuffers();
    [PreserveSig] int GetTotalLength(out int pcbTotalLength);
    [PreserveSig] int CopyToBuffer(IMFMediaBuffer pBuffer);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("bf94c121-5b05-4e7c-bc22-201fd6396b14")]
internal interface IMFTransform
{
    [PreserveSig] int GetStreamLimits(out int inputMin, out int inputMax, out int outputMin, out int outputMax);
    [PreserveSig] int GetStreamCount(out int inputStreams, out int outputStreams);
    [PreserveSig] int GetStreamIDs(int inputSize, int[] inputIds, int outputSize, int[] outputIds);
    [PreserveSig] int GetInputStreamInfo(int inputStreamId, out MftInputStreamInfo info);
    [PreserveSig] int GetOutputStreamInfo(int outputStreamId, out MftOutputStreamInfo info);
    [PreserveSig] int GetAttributes(out IMFAttributes attributes);
    [PreserveSig] int GetInputStreamAttributes(int inputStreamId, out IMFAttributes attributes);
    [PreserveSig] int GetOutputStreamAttributes(int outputStreamId, out IMFAttributes attributes);
    [PreserveSig] int DeleteInputStream(int streamId);
    [PreserveSig] int AddInputStreams(int count, int[] streamIds);
    [PreserveSig] int GetInputAvailableType(int inputStreamId, int typeIndex, out IMFMediaType type);
    [PreserveSig] int GetOutputAvailableType(int outputStreamId, int typeIndex, out IMFMediaType type);
    [PreserveSig] int SetInputType(int inputStreamId, IMFMediaType type, int flags);
    [PreserveSig] int SetOutputType(int outputStreamId, IMFMediaType type, int flags);
    [PreserveSig] int GetInputCurrentType(int inputStreamId, out IMFMediaType type);
    [PreserveSig] int GetOutputCurrentType(int outputStreamId, out IMFMediaType type);
    [PreserveSig] int GetInputStatus(int inputStreamId, out int flags);
    [PreserveSig] int GetOutputStatus(out int flags);
    [PreserveSig] int SetOutputBounds(long lower, long upper);
    [PreserveSig] int ProcessEvent(int inputStreamId, IntPtr mediaEvent);
    [PreserveSig] int ProcessMessage(int message, IntPtr param);
    [PreserveSig] int ProcessInput(int inputStreamId, IMFSample sample, int flags);
    [PreserveSig] int ProcessOutput(int flags, int count, [In] [Out] MftOutputDataBuffer[] buffers, out int status);
}

internal static class MfPlat
{
    public const int Version = 0x00020070;
    public const int BeginStreaming = 0x10000005;
    public const int StartOfStream = 0x10000006;
    public const int ProvidesSamples = 0x00000100;
    public const int NeedMoreInput = unchecked((int)0xC00D6D72);
    public const int StreamChange = unchecked((int)0xC00D6D61);
    public const int Progressive = 2;

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateMediaType(out IMFMediaType type);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateMemoryBuffer(int maxLength, out IMFMediaBuffer buffer);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateSample(out IMFSample sample);

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern int CoCreateInstance(
        in Guid clsid,
        IntPtr outer,
        uint clsContext,
        in Guid iid,
        out IntPtr instance);
}
