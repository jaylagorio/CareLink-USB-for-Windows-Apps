using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using Jungo.wdapi_dotnet;

using DWORD = System.UInt32;
using WORD = System.UInt16;
using WDU_DEVICE_HANDLE = System.IntPtr;
using UCHAR = System.Byte;

namespace Jungo.usb_lib 
{
    public delegate void D_USER_TRANSFER_COMPLETION(UsbPipe usbPipe);

    public class UsbPipe
    {
        private WDU_PIPE_INFO m_pPipe;
        private bool m_fInUse = false;
        private D_USER_TRANSFER_COMPLETION m_TransferCompletion;
        private Thread m_transThread = null;
        private WDU_DEVICE_HANDLE m_hDev;
        private DWORD m_dwTransferStatus;
        private DWORD m_dwBytesTransferred; 
        private bool m_fRead;
        private DWORD m_dwOptions;
        private byte[] m_buffer;
        private DWORD m_dwBuffSize;
        private byte[] m_SetupPacket;
        private DWORD m_timeOut;
        private bool m_bIsContiguous = false;

        internal UsbPipe(WDU_PIPE_INFO pipe, WDU_DEVICE_HANDLE hDev)
        {
            m_pPipe = pipe;
            m_hDev = hDev;
        }

        internal void Dispose()
        {
            if (m_transThread != null && m_transThread.IsAlive)
            {
                HaltTransferOnPipe();
                m_transThread.Abort();
                m_transThread.Join();
            }
        }

        public WDU_PIPE_INFO GetPipeInfo()
        {
            return m_pPipe;
        }

        public DWORD GetPipeNum()
        {
            return m_pPipe.dwNumber;
        }

        public DWORD GetPipeMaxPacketSz()
        {
            return m_pPipe.dwMaximumPacketSize;
        }

        public DWORD GetPipeDirection()
        {
            return m_pPipe.direction;
        }

        public DWORD GetPipeInterval()
        {
            return m_pPipe.dwInterval;
        }

        public DWORD GetPipeType()
        {
            return m_pPipe.type;
        }

        public bool IsPipeDirectionIn()
        {
            return (m_pPipe.direction == (DWORD)WDU_DIR.WDU_DIR_IN);
        }

        public bool IsPipeDirectionOut()
        {
            return (m_pPipe.direction == (DWORD)WDU_DIR.WDU_DIR_OUT);
        }

        public bool IsPipeDirectionInOut()
        {
            return (m_pPipe.direction == (DWORD)WDU_DIR.WDU_DIR_IN_OUT);
        }

        public bool IsControlPipe()
        {
            return (m_pPipe.type == (DWORD)USB_PIPE_TYPE.PIPE_TYPE_CONTROL);
        }

        public bool IsBulkPipe()
        {
            return (m_pPipe.type == (DWORD)USB_PIPE_TYPE.PIPE_TYPE_BULK);
        }

        public bool IsInterruptPipe()
        {
            return (m_pPipe.type == (DWORD)USB_PIPE_TYPE.PIPE_TYPE_INTERRUPT);
        }

        public bool IsIsochronousPipe()
        {
            return (m_pPipe.type == (DWORD)USB_PIPE_TYPE.PIPE_TYPE_ISOCHRONOUS);
        }

        public bool IsInUse()
        {
            return m_fInUse;
        }

        public bool GetfRead()
        {
            return m_fRead;
        }

        public WDU_DEVICE_HANDLE GethDev()
        {
            return m_hDev;
        }

        public DWORD GetTransferStatus()
        {
            return m_dwTransferStatus;
        }

        public DWORD GetBytesTransferred()
        {
            return m_dwBytesTransferred;
        }

        public DWORD GetBuffSize()
        {
            return m_dwBuffSize;
        }

        public DWORD GetTimeOut()
        {
            return m_timeOut;
        }

        public byte[] GetBuffer()
        {
            return m_buffer;
        }

        public byte[]GetSetupPacket()
        {
            return m_SetupPacket;
        }

        public void SetContiguous(bool bIsContiguous)
        {
            m_bIsContiguous = bIsContiguous;
        }

        public bool IsContiguous()
        {
            return m_bIsContiguous;
        }

        public DWORD UsbPipeTransfer(bool fRead, DWORD dwOptions, byte[] buffer,
        DWORD dwBuffSize, ref DWORD dwBytesTransfered, byte[] pSetupPacket,
        DWORD timeOut)
        {
            dwOptions |= (m_pPipe.type == 
                (DWORD)USB_PIPE_TYPE.PIPE_TYPE_ISOCHRONOUS)?
                (DWORD)TRANSFER_OPTIONS.USB_ISOCH_FULL_PACKETS_ONLY : 0;

            m_fRead = fRead;    
            m_fInUse = true;

            m_dwTransferStatus = wdu_lib_decl.WDU_Transfer(m_hDev, 
            m_pPipe.dwNumber, (DWORD)(fRead==true?1:0), dwOptions, buffer, 
            dwBuffSize, ref dwBytesTransfered, pSetupPacket, timeOut);

            if(!m_bIsContiguous)
                m_fInUse = false;        
            m_buffer = buffer;
            m_dwBytesTransferred = dwBytesTransfered;
            return m_dwTransferStatus;
        }

        void AsyncTransfer()
        {
            do
            {
                m_dwTransferStatus = UsbPipeTransfer(m_fRead, m_dwOptions,
                    m_buffer, m_dwBuffSize, ref m_dwBytesTransferred, 
                    m_SetupPacket, m_timeOut);
                if  (m_dwTransferStatus != (DWORD)WD_ERROR_CODES.WD_STATUS_SUCCESS)
                {
                    m_bIsContiguous = false;
                    m_fInUse = false;
                }
                m_TransferCompletion(this);
            }while (m_bIsContiguous);

        }

        public void UsbPipeTransferAsync(bool fRead, DWORD dwOptions,
            byte[] buffer, DWORD dwBuffSize, DWORD timeOut,
            D_USER_TRANSFER_COMPLETION TransferCompletion)
        {
            m_fRead = fRead;
            m_dwOptions = dwOptions;
            m_buffer = buffer;
            m_dwBuffSize = dwBuffSize;
            m_SetupPacket = null;
            m_timeOut = timeOut;
            m_TransferCompletion = TransferCompletion;
            m_fInUse = true;
            m_transThread = new Thread(new ThreadStart(AsyncTransfer));
            m_transThread.Start();                        
        }

        public void UsbPipeTransferAsync(bool fRead, DWORD dwOptions, 
            DWORD timeOut, D_USER_TRANSFER_COMPLETION TransferCompletion)
        {
            m_fRead = fRead;
            m_dwOptions = dwOptions;
            m_timeOut = timeOut;
            m_TransferCompletion = TransferCompletion;

            m_dwBuffSize = (DWORD)((m_pPipe.type !=
                (DWORD)USB_PIPE_TYPE.PIPE_TYPE_ISOCHRONOUS)? 
                m_pPipe.dwMaximumPacketSize : m_pPipe.dwMaximumPacketSize * 8);
            m_buffer = new byte[m_dwBuffSize];

            m_SetupPacket = null;
            m_fInUse = true;
            m_transThread = new Thread(new ThreadStart(AsyncTransfer));
            m_transThread.Start();

        }

        public DWORD HaltTransferOnPipe()
        {
            m_fInUse = false;
            return wdu_lib_decl.WDU_HaltTransfer(m_hDev, GetPipeNum());

        }

        public DWORD ResetPipe()
        {
            return wdu_lib_decl.WDU_ResetPipe(m_hDev, GetPipeNum());
        }

    };

    public class PipeList : ArrayList
    {
        internal PipeList(WDU_PIPE_INFO pPipe0, WDU_ALTERNATE_SETTING
            pActiveAltSetting, WDU_DEVICE_HANDLE hDev)
        {
            WDU_PIPE_INFO pipe_info;
            DWORD dwPipeIndex=0;
            DWORD dwNumOfPipes = pActiveAltSetting.Descriptor.bNumEndpoints;
            DWORD dwPipeSize = (DWORD)Marshal.SizeOf(typeof(WDU_PIPE_INFO));

            //inserting the control pipe to the list
            this.Insert(0, new UsbPipe(pPipe0, hDev));

            //retrieving the rest of the pipes from the active alternating 
            //settings struct and inserting them into the pipes' list 
            for(dwPipeIndex = 0; dwPipeIndex < dwNumOfPipes; ++dwPipeIndex)
            {
                pipe_info = (WDU_PIPE_INFO)Convert.ChangeType
                    (Marshal.PtrToStructure(new 
                        IntPtr(pActiveAltSetting.pPipes.ToInt64() 
                    + dwPipeIndex * dwPipeSize), typeof(WDU_PIPE_INFO)),
                        typeof(WDU_PIPE_INFO));
                UsbPipe pipe = new UsbPipe (pipe_info, hDev);

                this.Insert((int)dwPipeIndex + 1, pipe);
            }                
        }

        internal void Dispose()
        {
            for(int pipeIndex=0; pipeIndex < this.Count; ++pipeIndex)
                ((UsbPipe)this[pipeIndex]).Dispose();
        }
    };

    public class UsbDevice
    {
        private WDU_DEVICE_HANDLE hDevice;
        private WORD wVid;
        private WORD wPid;
        private DWORD dwInterfaceNum; // Interface number currently used by the app
        private DWORD dwAltSettingNum; // The active setting of the currently used interface
        private DWORD dwAddr = VALUE_NONE;
        private DWORD dwNumOfInterfaces; 
        private DWORD dwNumOfAltSettingsTotal; 
        private PipeList pPipesList = null; // A list of the pipes of the currently used interface
        private const DWORD VALUE_NONE = 0xffffffff;

        struct INTERFACE_INFO
        {
            public WDU_INTERFACE wduInterface;
            public UCHAR bInterfaceNumber; // Keep a local copy to avoid marshalling
        }

        private INTERFACE_INFO[] interfacesInfo;

        internal UsbDevice(WDU_DEVICE_HANDLE hDev, ref WDU_DEVICE pDeviceInfo)
        {
            WDU_CONFIGURATION pConfig = (WDU_CONFIGURATION)Marshal.PtrToStructure(pDeviceInfo.pActiveConfig,
                typeof(WDU_CONFIGURATION));

            dwNumOfInterfaces = pConfig.dwNumInterfaces;
            interfacesInfo = new INTERFACE_INFO[dwNumOfInterfaces];
            hDevice = hDev;

            for (uint i=0; i<dwNumOfInterfaces; ++i)
            {
                interfacesInfo[i].wduInterface = (WDU_INTERFACE)Marshal.PtrToStructure(
                    pDeviceInfo.pActiveInterface(i), typeof(WDU_INTERFACE));
                WDU_ALTERNATE_SETTING pAltSetting = (WDU_ALTERNATE_SETTING)Marshal.PtrToStructure(
                    interfacesInfo[i].wduInterface.pActiveAltSetting, typeof(WDU_ALTERNATE_SETTING));

                if(i == 0)
                {
                    dwInterfaceNum = pAltSetting.Descriptor.bInterfaceNumber;
                    dwAltSettingNum = pAltSetting.Descriptor.bAlternateSetting;
                    pPipesList = new PipeList(pDeviceInfo.Pipe0, pAltSetting, hDevice);
                }

                interfacesInfo[i].bInterfaceNumber = pAltSetting.Descriptor.bInterfaceNumber;
                
                dwNumOfAltSettingsTotal += interfacesInfo[i].wduInterface.dwNumAltSettings;
            }

            DWORD dwStatus = wdu_lib_decl.WDU_GetDeviceAddr(hDevice,
                ref dwAddr);

            wVid = (WORD)pDeviceInfo.Descriptor.idVendor; 
            wPid = (WORD)pDeviceInfo.Descriptor.idProduct;
        }

        internal void Dispose()
        {
            pPipesList.Dispose();
        }

        internal WDU_DEVICE_HANDLE GethDevice()
        {
            return hDevice;
        }

        internal int GetInterfaceIndexByNumber(DWORD dwInterfaceNumber)
        {
            for(int i=0; i<dwNumOfInterfaces; ++i)
            {
                if(interfacesInfo[i].bInterfaceNumber == dwInterfaceNumber)
                    return i;
            }

            return -1;
        }

        public UCHAR GetInterfaceNumberByIndex(uint index)
        {
            return interfacesInfo[index].bInterfaceNumber;
        }

        public WORD GetVid()
        {
            return wVid;
        }

        public WORD GetPid()
        {
            return wPid;
        }

        public DWORD GetCurrInterfaceNum()
        {
            return dwInterfaceNum;
        }

        public DWORD GetCurrInterfaceIndex()
        {
            return (DWORD)GetInterfaceIndexByNumber(dwInterfaceNum);
        }

        public DWORD GetCurrAlternateSettingNum()
        {
            return dwAltSettingNum;
        }

        public DWORD GetNumOfInteraces()
        {
            return dwNumOfInterfaces;
        }

        public DWORD GetNumOfAlternateSettingsPerInterface(DWORD dwInterfaceNumber)
        {
            int i = GetInterfaceIndexByNumber(dwInterfaceNumber);
            if (i != -1)
                return interfacesInfo[i].wduInterface.dwNumAltSettings;
            else return 0;
        }

        public DWORD GetNumOfAlternateSettingsTotal()
        {
            return dwNumOfAltSettingsTotal;
        }

        public PipeList GetpPipesList()
        {
            return pPipesList;
        }

        public DWORD ResetDevice(DWORD dwOptions)
        {
            return wdu_lib_decl.WDU_ResetDevice(hDevice, dwOptions);
        }

        public DWORD ChangeAlternateSetting(DWORD newInterface, DWORD newSetting)
        {
            DWORD dwStatus = (DWORD)WD_ERROR_CODES.WD_STATUS_SUCCESS;

            // if the chosen setting is the same as it was - do nothing
            if(newInterface == this.dwInterfaceNum && newSetting == this.dwAltSettingNum)
                return dwStatus;

            dwStatus = wdu_lib_decl.WDU_SetInterface(hDevice, newInterface,
                newSetting);
            if (dwStatus != (DWORD)WD_ERROR_CODES.WD_STATUS_SUCCESS)
                return dwStatus;

            IntPtr ppDeviceInfo=(System.IntPtr)0; 
            dwStatus = (DWORD)wdu_lib_decl.WDU_GetDeviceInfo(hDevice, 
                ref ppDeviceInfo);
            if (dwStatus != (DWORD)WD_ERROR_CODES.WD_STATUS_SUCCESS)
                return dwStatus;

            WDU_DEVICE pDeviceInfo = (WDU_DEVICE)Marshal.PtrToStructure(ppDeviceInfo,
                typeof(WDU_DEVICE));

            int index = GetInterfaceIndexByNumber(newInterface);
            interfacesInfo[index].wduInterface = (WDU_INTERFACE)Marshal.PtrToStructure(
                pDeviceInfo.pActiveInterface((uint)index), typeof(WDU_INTERFACE));

            WDU_ALTERNATE_SETTING pActiveAltSetting =
                (WDU_ALTERNATE_SETTING)Marshal.PtrToStructure(interfacesInfo[index].
                    wduInterface.pActiveAltSetting, typeof(WDU_ALTERNATE_SETTING));
            dwInterfaceNum = newInterface;
            dwAltSettingNum = newSetting;
            pPipesList = new PipeList(((UsbPipe)(pPipesList[0])).GetPipeInfo(), 
                pActiveAltSetting, hDevice); 
            wdu_lib_decl.WDU_PutDeviceInfo(ppDeviceInfo);

            return dwStatus;
        }

        public bool IsDeviceTransferring()
        {
            for(int i = 0; i < pPipesList.Count; ++i)
                if(((UsbPipe)pPipesList[i]).IsInUse())
                    return true;
            return false;
        }

        public string DeviceDescription()
        {
            string deviceDesc = "Device ";
            if (dwAddr != VALUE_NONE)
                string.Concat(deviceDesc, "0x" , dwAddr.ToString("X") , ", ");

            return string.Concat(deviceDesc, "vid 0x", wVid.ToString("X"),
                ", pid 0x", wPid.ToString("X"), ", ifc ",
                dwInterfaceNum.ToString(), ", alt setting ", 
                dwAltSettingNum.ToString(), ", handle 0x", 
                hDevice.ToString());
        }

        public static bool operator ==(UsbDevice u1, UsbDevice u2) 
        {
            try
            {
                return (u1.hDevice == u2.hDevice);
            }
            catch 
            {
                return false;
            }

        }

        public static bool operator !=(UsbDevice u1, UsbDevice u2) 
        {
            return !(u1 == u2);
        }

        public override bool Equals(object obj)
        {
            try
            {
                return (bool)(this == (UsbDevice)obj);
            }
            catch
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return (int)hDevice;
        }
    };
}
