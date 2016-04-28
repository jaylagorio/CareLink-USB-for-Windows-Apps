Imports Jungo.usb_lib
Imports Jungo.wdapi_dotnet
Imports wdu_err = Jungo.wdapi_dotnet.WD_ERROR_CODES
Module CareLinkUSBJungoControl
    ' Driver/Device magic values
    Private Const DEFAULT_LICENSE_STRING As String = "0123456789abcdef.vendor"
    Private Const DEFAULT_DRIVER_NAME As String = "windrvr6"
    Private Const DEFAULT_VENDOR_ID As UShort = &HA21
    Private Const DEFAULT_PRODUCT_ID As UShort = &H8001
    Public Const TIME_OUT As Int32 = 30000

    ' Device management objects and callbacks
    Private DeviceManager As UsbDeviceManager
    Private Delegate Sub D_ATTACH_GUI_CALLBACK(ByVal pDev As UsbDevice)
    Private Delegate Sub D_DETACH_GUI_CALLBACK(ByVal pDev As UsbDevice)

    ' Connected device variables
    Private DeviceConnected As Boolean
    Private Device As UsbDevice
    Dim ReadPipe As UsbPipe = Nothing
    Dim WritePipe As UsbPipe = Nothing

    Sub Main()
        ' Setup device event callback functions
        Dim DeviceAttachCallback As D_USER_ATTACH_CALLBACK = AddressOf DeviceAttach
        Dim DeviceDetachCallback As D_USER_DETACH_CALLBACK = AddressOf DeviceDetach

        ' Initialize the USB device manager. This needs callbacks to call when devices attach and detach, the
        ' device vendor and product ID, the WinDriver name, and Medtronic's WinDriver license data.
        DeviceManager = New UsbDeviceManager(DeviceAttachCallback, DeviceDetachCallback, Convert.ToUInt16(DEFAULT_VENDOR_ID), Convert.ToUInt16(DEFAULT_PRODUCT_ID), DEFAULT_DRIVER_NAME, DEFAULT_LICENSE_STRING)

        ' Wait for a device to attach - unless there's already a device attached when it runs
        Console.WriteLine("Waiting for device to be attached...")
        While Not DeviceConnected
            Call Threading.Thread.Yield()
        End While

        ' Device is connected, find the read and write pipes
        ' and save them to the global variables
        Dim PipesList As PipeList = Device.GetpPipesList()
        For Each Pipe As UsbPipe In PipesList
            If Pipe.IsPipeDirectionIn Then
                ReadPipe = Pipe
            ElseIf Pipe.IsPipeDirectionOut Then
                WritePipe = Pipe
            End If
        Next

        If Not WritePipe Is Nothing And Not ReadPipe Is Nothing Then
            Console.WriteLine("Starting listener pipe...")
            ' If the pipes are found setup the read pipe to listen for device data with the
            ' ListenCompletion function as a callback for when data is received
            Dim ListenerCompletion As D_USER_TRANSFER_COMPLETION = AddressOf ListenCompletion

            ' If the read pipe isn't in use queue the non-blocking read request
            If Not ReadPipe.IsInUse Then
                Dim dwOptions As UInt32 = Convert.ToUInt32(0)
                ReadPipe.SetContiguous(True)
                ReadPipe.UsbPipeTransferAsync(True, dwOptions, Convert.ToUInt32(TIME_OUT), ListenerCompletion)
            End If

            ' Write the ProductInfo opcode to the pipe, which should cause the
            ' stick to send back data on the listening pipe.
            Console.WriteLine("Starting write operation...")
            Call WriteToStick()
        End If

        ' Keep the console window alive until the user presses a key
        Console.ReadKey()
    End Sub

    ' Event runs when a device is attached or the initial enumeration
    ' happens when a device is already attached
    Private Sub DeviceAttach(ByVal UsbDevice As UsbDevice)
        Console.WriteLine("Device found: " & UsbDevice.DeviceDescription)

        ' Save the USB device to the global variable and mark it connected
        Device = UsbDevice
        DeviceConnected = True
    End Sub

    ' Event runs when a device is removed from the system
    Private Sub DeviceDetach(ByVal UsbDevice As UsbDevice)
        Console.WriteLine("Device removed: " & UsbDevice.DeviceDescription)

        ' Mark the device as disconnected and clear the global variable
        DeviceConnected = False
        Device = Nothing
    End Sub

    ' Function runs when data comes into a listening pipe
    Private Sub ListenCompletion(ByVal ListeningPipe As UsbPipe)
        ' Check the status of the listen operation
        Dim dwStatus As UInt32 = ListeningPipe.GetTransferStatus()

        ' Check the error code, whether the listen was cancelled, and whether the read was contiguous
        Dim IsListenStopped As Boolean = ((Convert.ToInt64(dwStatus) = wdu_err.WD_IRP_CANCELED) AndAlso (Not ListeningPipe.IsContiguous()))

        ' Finally check for success and whether the pipe is still listening
        If (Convert.ToInt64(dwStatus) <> wdu_err.WD_STATUS_SUCCESS AndAlso Not IsListenStopped) Then
            Console.WriteLine(String.Format("Listen Failed! Error {0}: {1} ", dwStatus.ToString("X"), utils.Stat2Str(dwStatus)))
        Else
            ' Print the buffer contents to the console
            Console.Write("Data received from stick: ")
            Console.WriteLine(String.Format("{0}", DisplayHexBuffer(ListeningPipe.GetBuffer(), ListeningPipe.GetBytesTransferred())))
        End If
    End Sub

    ' Function runs when writing to the CareLink stick completes
    Private Sub TransferCompletion(ByVal TransferPipe As UsbPipe)
        If (Convert.ToInt64(TransferPipe.GetTransferStatus()) <> wdu_err.WD_STATUS_SUCCESS) Then
            Dim dwStatus As UInt32 = TransferPipe.GetTransferStatus()
            Console.WriteLine(String.Format("USB Write Failed! Error {0}: {1} ", dwStatus.ToString("X"), utils.Stat2Str(dwStatus)))
        End If
    End Sub

    ' Write a two byte buffer (the ProductInfo opcode) to the CareLink stick with a
    ' 30 second timeout
    Private Sub WriteToStick()
        ' Buffer of bytes to write and an Integer with the number of bytes
        Dim Buffer() As Byte = {4, 0}
        Dim BufferSize As UInt32 = Convert.ToUInt32(Buffer.Count)

        ' No options selected, 30 second timeout
        Dim Options As UInt32 = Convert.ToUInt32(0)
        Dim TimeOut As UInt32 = Convert.ToUInt32(TIME_OUT)

        ' Function that runs when the write is completed
        Dim TransferCompletionFunction As D_USER_TRANSFER_COMPLETION = AddressOf TransferCompletion

        ' Write to the pipe
        WritePipe.UsbPipeTransferAsync(False, Options, Buffer, BufferSize, TimeOut, TransferCompletionFunction)

        Debug.WriteLine("Data written to pipe.")
    End Sub

    ' Converts the byte array to a hex string 
    Private Function DisplayHexBuffer(ByVal buff() As Byte, ByVal dwBuffSize As UInt32) As String
        Dim i As Int32
        Dim display As String = ""
        For i = 0 To (Convert.ToInt32(dwBuffSize) - 1)
            If buff(i) < 16 Then
                display = String.Concat(display, "0", buff(i).ToString("X"), " ")
            Else
                display = String.Concat(display, buff(i).ToString("X"), " ")
            End If
        Next i

        display = String.Concat(display, Environment.NewLine)
        Return display
    End Function
End Module
