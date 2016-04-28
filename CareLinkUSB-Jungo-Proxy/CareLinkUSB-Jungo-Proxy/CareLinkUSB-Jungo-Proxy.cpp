// CareLinkUSB-Jungo-Proxy.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "CareLinkUSB-Jungo-Proxy.h"
#include <jni.h>
#include <stdlib.h>

// Log file handle
HANDLE hLogFile = INVALID_HANDLE_VALUE;

// Module handle for the proxied library
HMODULE hDestLibrary = NULL;

// Function signatures of JNI-compliant functions in the real library
typedef jintArray(WINAPI* f_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles)(JNIEnv *env, jobject obj);
typedef void (WINAPI* f_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes)(JNIEnv *env, jobject obj, int handle);
typedef void (WINAPI* f_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative)(JNIEnv *env, jobject obj, int paramInt1, jbyteArray bytes, int paramInt2, int sizeToRead);
typedef void (WINAPI* f_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative)(JNIEnv *env, jobject obj, int handle, jbyteArray bytes);

// Function pointers to call into the proxied library
f_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles p_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles;
f_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes p_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes;
f_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative p_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative;
f_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative p_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative;

// Return an array of integers which are handles to the device
CARELINKUSBJUNGOPROXY_API jintArray __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles(JNIEnv * env, jobject obj)
{
	Log(L"getDeviceHandles: Called");

	// Attempt to proxy the library, fail out if it doesn't work
	if (!LoadProxiedLibrary())
	{
		Log(L"getDeviceHandles: Failed to proxy DLL");
		return NULL;
	}

	// Get the array of handles (usually one int) for the device
	jintArray retval = p_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles(env, obj);

	// Get the size of the array
	int size = env->GetArrayLength(retval);
	jint *pHandles = env->GetIntArrayElements(retval, FALSE);

	// Print the handles to the Debug log
	Log(L"Handles retrieved: ", size);
	for (int i = 0; i < size; i++)
	{
		Log(L"Handle: 0x%x", pHandles[i]);
	}

	// Release resources
	env->ReleaseIntArrayElements(retval, pHandles, 0);

	// Return the result from the real DLL
	return retval;
}

// Resets the pipes associated with the handle, which is retrieved from getDeviceHandles.
CARELINKUSBJUNGOPROXY_API void __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes(JNIEnv * env, jobject obj, int handle)
{
	Log(L"resetPipes: Called");

	// Attempt to proxy the target library
	if (!LoadProxiedLibrary())
	{
		Log(L"resetPipes: Failed to proxy DLL");
		return;
	}

	Log(L"resetPipes: Resetting handle 0x%x", handle);

	// Reset the pipe. No return data.
	p_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes(env, obj, handle);
	return;
}

/*
Reads data from the CareLink stick and passes back to the Java applet.
JNIEnv *env: Java JNI environment pointer
jobject obj: Pointer to "this"
int handle: Handle from getDeviceHandles()
jbyteArray bytes: An array of allocated bytes, sized to sizeToRead
int paramInt2: Always 0
int sizeToRead: The number of bytes to read, which is the allocation size of the bytes parameter
*/
CARELINKUSBJUNGOPROXY_API void __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative(JNIEnv * env, jobject obj, int handle, jbyteArray bytes, int paramInt2, int sizeToRead)
{
	Log(L"readNative: Called");

	// Attempt to proxy the library
	if (!LoadProxiedLibrary())
	{
		Log(L"readNative: Failed to proxy DLL");
		return;
	}

	// Do the read request and hold the data retrieved
	p_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative(env, obj, handle, bytes, paramInt2, sizeToRead);

	// Spit out some debug log data
	Log(L"readNative: handle: 0x%x", handle);
	Log(L"readNative: bytes length: %d", env->GetArrayLength(bytes));
	Log(L"readNative: paramInt2: 0x%x", paramInt2);
	Log(L"readNative: sizeToRead: %d", sizeToRead);

	// Verify the log file is open
	DWORD bw = 0;
	if (OpenLogFile())
	{
		// Take the bytes read off the wire and put them in the log file
		if (!WriteLogFile("readNative : ", env, bytes, true))
		{
			Log(L"readNative: Failed to write bytes to log file");
		}
	}
	else
	{
		Log(L"readNative: Failed to open log file");
	}

	return;
}

// Writes bytes to the CareLink stick
// JNIEnv *env: The Java JNI environment pointer
// jobject obj: The this pointer from Java
// int handle: Handle to write the bytes to
// jbyteArray bytes: Java array of bytes to write to the Carelink stick
CARELINKUSBJUNGOPROXY_API void __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative(JNIEnv * env, jobject obj, int handle, jbyteArray bytes)
{
	Log(L"writeNative: Called");

	// Attempt to proxy the library
	if (!LoadProxiedLibrary())
	{
		Log(L"writeNative: Failed to proxy DLL");
		return;
	}

	// Debug log the handle that was used to write
	Log(L"writeNative: handle: 0x%x", handle);

	DWORD bw = 0;
	// Attempt to open the log file and write the bytes sent down the wire
	if (OpenLogFile())
	{
		if (!WriteLogFile("writeNative: ", env, bytes, false))
		{
			Log(L"writeNative: Failed to write bytes to log file");
		}
	}
	else
	{
		Log(L"writeNative: Failed to open log file");
	}

	p_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative(env, obj, handle, bytes);
	return;
}

// This function attempts to load the proxied library if it hasn't
// already been done.
bool LoadProxiedLibrary()
{
	// If the library is already loaded skip all of this
	if (!IsLibraryProxied())
	{
		Log(L"Going to proxy library");

		// Depending on the architecture give the complete path to the library
		// to be loaded. Even though these libraries should be in the same directory
		// as the proxy library the filename alone wasn't sufficient to get it loaded.
#ifndef WIN64
		hDestLibrary = LoadLibrary(L"C:\\ProgramData\\Medtronic\\ddmsDTWusb\\ComLink2\\cl2_jni_wrapper_real.dll");
#else
		hDestLibrary = LoadLibrary(L"C:\\ProgramData\\Medtronic\\ddmsDTWusb\\ComLink2\\cl2_jni_wrapper_64_real.dll");
#endif
		if (hDestLibrary)
		{
			Log(L"Library loaded");

			// If the library was loaded start looking for the exported functions. The function names
			// are different because in x86 the functions are __stdcall (indicated by the
			// prepended _ and appended @##) and the x64 functions are __fastcall.
#ifndef WIN64
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles)GetProcAddress(hDestLibrary, "Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles");
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes)GetProcAddress(hDestLibrary, "Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes");
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative)GetProcAddress(hDestLibrary, "Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative");
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative)GetProcAddress(hDestLibrary, "Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative");
#else
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles)GetProcAddress(hDestLibrary, "_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles@8");
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes)GetProcAddress(hDestLibrary, "_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes@12");
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative)GetProcAddress(hDestLibrary, "_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative@24");
			p_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative = (f_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative)GetProcAddress(hDestLibrary, "_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative@16");
#endif
			// Check to see that all functions were found and can be called later
			if (p_Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles &&
				p_Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes &&
				p_Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative &&
				p_Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative)
			{
				Log(L"Proxy success");
				return true;
			}
			else
			{
				// If a function was not found as expected free the library and NULL the handle
				Log(L"Couldn't find functions");
				FreeLibrary(hDestLibrary);
				hDestLibrary = NULL;
			}
		}
		else
		{
			Log(L"Couldn't load library: %d", GetLastError());
		}
	}
	else
	{
		return true;
	}

	return false;
}

// Quick check to make sure we don't try to reload the target DLL
bool IsLibraryProxied()
{
	return (hDestLibrary != NULL);
}

// Print a message to the system debug interface
void Log(LPCWSTR lpstrMessage)
{
	OutputDebugString(lpstrMessage);
	return;
}

// Print a message with a value to the system debug interface. The fully formed
// message must be less than MAX_PATH.
void Log(LPCWSTR lpstrMessage, DWORD dwCode)
{
	LPWSTR lpstrCompleteMessage = (LPWSTR)malloc(MAX_PATH);
	wsprintf(lpstrCompleteMessage, lpstrMessage, dwCode);
	Log(lpstrCompleteMessage);
	free(lpstrCompleteMessage);
	lpstrCompleteMessage = NULL;
}

// Open the log file if it hasn't already been opened
bool OpenLogFile()
{
	if (hLogFile == INVALID_HANDLE_VALUE)
	{
		// Write permission only, allow other programs to open for read. Open existing files
		// if necessary or create a new one if it doesn't already exist.
		hLogFile = CreateFile(L"C:\\ProgramData\\Medtronic\\ddmsDTWusb\\ComLink2\\Log.txt", GENERIC_WRITE, FILE_SHARE_READ, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
		if (hLogFile == INVALID_HANDLE_VALUE)
		{
			return false;
		}
	}
	else
	{
		return true;
	}

	return true;
}

// Writes an array of bytes from Java to the log file.
// const char* caller: The name of the calling function (prepended to the bytes written)
// JNIEnv *env: Java environment
// jbyteArray bytes: An array of bytes to write to the log file
// bool bExtraCRLF: Adds an extra new line after the array is written (to seperate write/read blocks)
bool WriteLogFile(const char* caller, JNIEnv *env, jbyteArray bytes, bool bExtraCRLF)
{
	DWORD bw = 0;

	// Get the size of the byte array to write
	int writeSize = env->GetArrayLength(bytes);

	// Write the name of the calling function
	WriteFile(hLogFile, caller, (DWORD)strlen(caller), &bw, NULL);

	// Get the byte array
	jbyte* cbytes = env->GetByteArrayElements(bytes, false);
	if (cbytes)
	{
		char thisbyte[10] = { 0 };

		for (int i = 0; i < writeSize; i++)
		{
			// Zero the string to write and make sure
			// higher than 8 bits are lopped off
			ZeroMemory(thisbyte, 10);
			BYTE b = cbytes[i] & 0xff;

			// Format the number to be a two-digit hex number
			sprintf_s((LPSTR)&thisbyte, 10, "%2.2x ", b);
			WriteFile(hLogFile, &thisbyte, (DWORD)strlen(thisbyte), &bw, NULL);
		}

		// Write a new line, and write a second one if specified
		WriteFile(hLogFile, "\r\n", (DWORD)strlen("\r\n"), &bw, NULL);
		if (bExtraCRLF)
		{
			WriteFile(hLogFile, "\r\n", (DWORD)strlen("\r\n"), &bw, NULL);
		}

		// Release resources
		env->ReleaseByteArrayElements(bytes, cbytes, 0);
	}
	return true;
}