// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the CARELINKUSBJUNGOPROXY_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// CARELINKUSBJUNGOPROXY_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef CARELINKUSBJUNGOPROXY_EXPORTS
#define CARELINKUSBJUNGOPROXY_API __declspec(dllexport)
#else
#define CARELINKUSBJUNGOPROXY_API __declspec(dllimport)
#endif

#include <jni.h>

extern "C" {
	CARELINKUSBJUNGOPROXY_API jintArray __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_getDeviceHandles(JNIEnv *env, jobject obj);
	CARELINKUSBJUNGOPROXY_API void __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_resetPipes(JNIEnv *env, jobject obj, int handle);
	CARELINKUSBJUNGOPROXY_API void __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_readNative(JNIEnv *env, jobject obj, int paramInt1, jbyteArray bytes, int paramInt2, int sizeToRead);
	CARELINKUSBJUNGOPROXY_API void __stdcall Java_mdt_common_device_driver_minimed_JungoUSBPort_writeNative(JNIEnv *env, jobject obj, int handle, jbyteArray bytes);
};

bool LoadProxiedLibrary();
bool IsLibraryProxied();
void Log(LPCWSTR lpstrMessage);
void Log(LPCWSTR lpstrMessage, DWORD dwCode);
bool OpenLogFile();
bool WriteLogFile(const char* caller, JNIEnv *env, jbyteArray bytes, bool bExtraCRLF);
