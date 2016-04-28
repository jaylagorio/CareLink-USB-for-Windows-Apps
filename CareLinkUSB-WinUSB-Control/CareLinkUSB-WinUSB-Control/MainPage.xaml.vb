Imports System.Threading
Imports Windows.Devices
Imports Windows.Devices.Usb
Imports Windows.Devices.Enumeration
' The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

''' <summary>
''' An empty page that can be used on its own or navigated to within a Frame.
''' </summary>
Public NotInheritable Class MainPage
    Inherits Page

    ' The Vendor and Product ID for the CareLink USB stick
    Dim VendorID As UShort = &HA21
    Dim ProductID As UShort = &H8001

    ' Global for the device when it's found so that it can be accessed across threads
    Dim Device As UsbDevice

    Private Async Sub MainPage_Loaded(sender As Object, e As RoutedEventArgs)
        Dim DeviceInfo As DeviceInformation = Nothing

        ' Find any CareLink sticks connected to the system
        Dim DevInfoCollection As DeviceInformationCollection = Await DeviceInformation.FindAllAsync(UsbDevice.GetDeviceSelector(VendorID, ProductID))
        For i = 0 To DevInfoCollection.Count - 1
            DeviceInfo = DevInfoCollection(i)
            Exit For
        Next

        ' Check to make sure a stick was found
        If Not DeviceInfo Is Nothing Then
            Device = Await UsbDevice.FromIdAsync(DeviceInfo.Id)

            ' If we aren't granted access to the device then FromIdAsync returns Nothing 
            If Not Device Is Nothing Then
                ' Create and run the WorkItem that takes care of reads from the device
                Dim ReadHandlerWorkItem As New Windows.System.Threading.WorkItemHandler(AddressOf ReadHandler)
                Dim AsyncAction As IAsyncAction = Windows.System.Threading.ThreadPool.RunAsync(ReadHandlerWorkItem)

                ' An opcode that generates a response from the device
                Dim ProductInfoOpcode() As Byte = {4, 0}

                ' There should only be one output pipe but we'll throw the opcode over
                ' whatever will allow it
                For Each Pipe As UsbBulkOutPipe In Device.DefaultInterface.BulkOutPipes
                    ' Create a DataWriter and throw the bytes
                    Dim DataWriter As New Windows.Storage.Streams.DataWriter(Pipe.OutputStream)
                    Call DataWriter.WriteBytes(ProductInfoOpcode)
                    Await DataWriter.StoreAsync()

                    ' Display the bytes we sent on the form
                    For i = 0 To ProductInfoOpcode.Count - 1
                        If ProductInfoOpcode(i) < 16 Then
                            BytesSent.Text &= "0" & ByteToHex(ProductInfoOpcode(i)) & " "
                        Else
                            BytesSent.Text &= ByteToHex(ProductInfoOpcode(i)) & " "
                        End If
                    Next
                Next
            End If
        End If
    End Sub

    Private Async Sub ReadHandler(operation As IAsyncAction)
        ' There should only be one read pipe but we'll try to read on as many
        ' as there are
        For Each Pipe As UsbBulkInPipe In Device.DefaultInterface.BulkInPipes
            ' Tell DataReader to block until it has any amount of bytes, not just the
            ' maximum number of the read operation
            Dim DataReader As New Windows.Storage.Streams.DataReader(Pipe.InputStream)
            DataReader.InputStreamOptions = Windows.Storage.Streams.InputStreamOptions.Partial

            ' Read from the device, copy into the byte buffer
            Dim BytesRead As Integer = Await DataReader.LoadAsync(Pipe.EndpointDescriptor.MaxPacketSize)
            Dim Bytes(BytesRead - 1) As Byte
            For i = 0 To Bytes.Count - 1
                Bytes(i) = DataReader.ReadByte()
            Next

            ' Write the bytes to a textblock on the form (on the UI thread)
            Await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, agileCallback:=
                           Sub()
                               For i = 0 To Bytes.Count - 1
                                   If Bytes(i) < 16 Then
                                       BytesReceived.Text &= "0" & ByteToHex(Bytes(i)) & " "
                                   Else
                                       BytesReceived.Text &= ByteToHex(Bytes(i)) & " "
                                   End If
                               Next
                           End Sub)
        Next
    End Sub

    ' Convert a byte to its two-digit hex representation
    Private Function ByteToHex(ByVal ByteToConvert As Byte) As String
        If ByteToConvert <= 10 Then
            Return ByteToConvert
        ElseIf ByteToConvert <= 16 Then
            Return ChrW(ByteToConvert + 55)
        Else
            Return ByteToHex((ByteToConvert And &HF0) >> 4) & ByteToHex(ByteToConvert And &HF)
        End If
    End Function
End Class
