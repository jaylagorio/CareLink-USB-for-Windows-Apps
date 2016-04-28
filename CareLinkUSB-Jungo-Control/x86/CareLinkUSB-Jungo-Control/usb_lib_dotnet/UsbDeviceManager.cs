using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Data;
using System.Threading;
using Jungo.wdapi_dotnet;

using DWORD = System.UInt32;
using WORD = System.UInt16;
using WDU_DEVICE_HANDLE = System.IntPtr;

namespace Jungo.usb_lib
{
    public delegate void D_USER_ATTACH_CALLBACK(UsbDevice pDev);
    public delegate void D_USER_DETACH_CALLBACK(UsbDevice pDev);
    public delegate void D_USER_POWER_CHANGE_CALLBCAK(UsbDevice pDev);

    public class UsbDeviceManager
    {
        WDU_DEVICE_HANDLE hDriver;
        private ArrayList pDevicesList = null;
        private D_USER_ATTACH_CALLBACK dUserAttachCb;
        private D_USER_DETACH_CALLBACK dUserDetachCb;
        private D_USER_POWER_CHANGE_CALLBCAK dUserPowerCb; 
        private WDU_EVENT_TABLE eventTable;
        private WDU_MATCH_TABLE[] matchTableArr = new WDU_MATCH_TABLE[1];
        private Mutex hMutex = null;

        public UsbDeviceManager(D_USER_ATTACH_CALLBACK dAttachCb,
            D_USER_DETACH_CALLBACK dDetachCb,
            D_USER_POWER_CHANGE_CALLBCAK dPowerCb,
            WORD wVendorId, WORD wProductId, string sDriverName, string lic)
        {
            DWORD dwStatus = 0;
            
            windrvr_decl.WD_DriverName(sDriverName);
            pDevicesList = new ArrayList();
            dUserAttachCb = dAttachCb;
            dUserDetachCb = dDetachCb;
            dUserPowerCb = dPowerCb; 

            matchTableArr[0].wVendorId = (WORD)wVendorId;
            matchTableArr[0].wProductId = (WORD)wProductId;

            eventTable = new WDU_EVENT_TABLE(new
                D_WDU_ATTACH_CALLBACK(DeviceAttach),
                new D_WDU_DETACH_CALLBACK(DeviceDetach));

            hMutex = new Mutex();
            dwStatus = wdu_lib_decl.WDU_Init(ref hDriver, matchTableArr,
                (DWORD)matchTableArr.Length, ref eventTable, lic,
                (DWORD)windrvr_consts.WD_ACKNOWLEDGE);

            if (WD_ERROR_CODES.WD_STATUS_SUCCESS != (WD_ERROR_CODES)dwStatus)
                hMutex.Close();
        }

        public UsbDeviceManager(D_USER_ATTACH_CALLBACK dAttachCb,
            D_USER_DETACH_CALLBACK dDetachCb, WORD wVendorId, 
            WORD wProductId, string sDriverName, string lic) : this(dAttachCb,
                dDetachCb, null, wVendorId, wProductId, sDriverName, lic)
        {
        }

        public void Dispose()
        {
            if (hMutex != null)
                hMutex.Close();

            for(int i=0; i < pDevicesList.Count; ++i)
                ((UsbDevice)pDevicesList[0]).Dispose();

            wdu_lib_decl.WDU_Uninit(hDriver);
        }

        public DWORD GetNumOfDevicesAttached()
        {
            return (DWORD)pDevicesList.Count;
        }

        private bool DeviceAttach(WDU_DEVICE_HANDLE hDevice, ref WDU_DEVICE
            pDeviceInfo, IntPtr pUserData)
        {
            UsbDevice pDev = new UsbDevice(hDevice,ref pDeviceInfo);

            hMutex.WaitOne();
            pDevicesList.Add(pDev);
            hMutex.ReleaseMutex();

            dUserAttachCb(pDev);
            return true;
        }

        private void DeviceDetach(WDU_DEVICE_HANDLE hDevice, IntPtr pUserData)
        {
            UsbDevice pDev=null;
            int index=0;

            hMutex.WaitOne();

            if(pDevicesList.Count == 0)
            {
                hMutex.ReleaseMutex();
                return;
            }

            for(index = 0; index < pDevicesList.Count ;++index)
            {
                pDev = (UsbDevice)pDevicesList[index];
                if(pDev.GethDevice() == hDevice)
                    break;
            }

            if(pDev != null)
            {
                pDevicesList.Remove(pDev);
            }

            hMutex.ReleaseMutex();
            dUserDetachCb(pDev);
            pDev.Dispose();
        }
    };
}
