/*
 * This file is auto-generated.  DO NOT MODIFY.
 * Using: C:\\Users\\mrlog\\AppData\\Local\\Android\\Sdk\\build-tools\\37.0.0\\aidl.exe -pC:\\Users\\mrlog\\AppData\\Local\\Android\\Sdk\\platforms\\android-37\\framework.aidl -IE:\\Repository\\launcher\\src\\Launcher\\Aidl -IC:\\Users\\mrlog\\AppData\\Local\\Android\\Sdk\\platforms\\android-37 -oE:\\Repository\\launcher\\src\\Launcher\\Java E:\\Repository\\launcher\\src\\Launcher\\Aidl\\com\\google\\android\\libraries\\launcherclient\\ILauncherOverlayCallback.aidl
 *
 * DO NOT CHECK THIS FILE INTO A CODE TREE (e.g. git, etc..).
 * ALWAYS GENERATE THIS FILE FROM UPDATED AIDL COMPILER
 * AS A BUILD INTERMEDIATE ONLY. THIS IS NOT SOURCE CODE.
 */
package com.google.android.libraries.launcherclient;
public interface ILauncherOverlayCallback extends android.os.IInterface
{
  /** Default implementation for ILauncherOverlayCallback. */
  public static class Default implements com.google.android.libraries.launcherclient.ILauncherOverlayCallback
  {
    @Override public void overlayScrollChanged(float progress) throws android.os.RemoteException
    {
    }
    @Override public void overlayStatusChanged(int status) throws android.os.RemoteException
    {
    }
    @Override
    public android.os.IBinder asBinder() {
      return null;
    }
  }
  /** Local-side IPC implementation stub class. */
  public static abstract class Stub extends android.os.Binder implements com.google.android.libraries.launcherclient.ILauncherOverlayCallback
  {
    /** Construct the stub and attach it to the interface. */
    @SuppressWarnings("this-escape")
    public Stub()
    {
      this.attachInterface(this, DESCRIPTOR);
    }
    /**
     * Cast an IBinder object into an com.google.android.libraries.launcherclient.ILauncherOverlayCallback interface,
     * generating a proxy if needed.
     */
    public static com.google.android.libraries.launcherclient.ILauncherOverlayCallback asInterface(android.os.IBinder obj)
    {
      if ((obj==null)) {
        return null;
      }
      android.os.IInterface iin = obj.queryLocalInterface(DESCRIPTOR);
      if (((iin!=null)&&(iin instanceof com.google.android.libraries.launcherclient.ILauncherOverlayCallback))) {
        return ((com.google.android.libraries.launcherclient.ILauncherOverlayCallback)iin);
      }
      return new com.google.android.libraries.launcherclient.ILauncherOverlayCallback.Stub.Proxy(obj);
    }
    @Override public android.os.IBinder asBinder()
    {
      return this;
    }
    @Override public boolean onTransact(int code, android.os.Parcel data, android.os.Parcel reply, int flags) throws android.os.RemoteException
    {
      if (code >= android.os.IBinder.FIRST_CALL_TRANSACTION && code <= android.os.IBinder.LAST_CALL_TRANSACTION) {
        data.enforceInterface(DESCRIPTOR);
      }
      switch (code)
      {
        case TRANSACTION_overlayScrollChanged:
        {
          float _arg0;
          _arg0 = data.readFloat();
          this.overlayScrollChanged(_arg0);
          break;
        }
        case TRANSACTION_overlayStatusChanged:
        {
          int _arg0;
          _arg0 = data.readInt();
          this.overlayStatusChanged(_arg0);
          break;
        }
        default:
        {
          return super.onTransact(code, data, reply, flags);
        }
      }
      return true;
    }
    private static final class Proxy implements com.google.android.libraries.launcherclient.ILauncherOverlayCallback
    {
      private android.os.IBinder mRemote;
      Proxy(android.os.IBinder remote)
      {
        mRemote = remote;
      }
      @Override public android.os.IBinder asBinder()
      {
        return mRemote;
      }
      public final java.lang.String getInterfaceDescriptor()
      {
        return DESCRIPTOR;
      }
      @Override public void overlayScrollChanged(float progress) throws android.os.RemoteException
      {
        android.os.Parcel _data = android.os.Parcel.obtain();
        try {
          _data.writeInterfaceToken(DESCRIPTOR);
          _data.writeFloat(progress);
          boolean _status = mRemote.transact(Stub.TRANSACTION_overlayScrollChanged, _data, null, android.os.IBinder.FLAG_ONEWAY);
        }
        finally {
          _data.recycle();
        }
      }
      @Override public void overlayStatusChanged(int status) throws android.os.RemoteException
      {
        android.os.Parcel _data = android.os.Parcel.obtain();
        try {
          _data.writeInterfaceToken(DESCRIPTOR);
          _data.writeInt(status);
          boolean _status = mRemote.transact(Stub.TRANSACTION_overlayStatusChanged, _data, null, android.os.IBinder.FLAG_ONEWAY);
        }
        finally {
          _data.recycle();
        }
      }
    }
    static final int TRANSACTION_overlayScrollChanged = (android.os.IBinder.FIRST_CALL_TRANSACTION + 0);
    static final int TRANSACTION_overlayStatusChanged = (android.os.IBinder.FIRST_CALL_TRANSACTION + 1);
  }
  /** @hide */
  public static final java.lang.String DESCRIPTOR = "com.google.android.libraries.launcherclient.ILauncherOverlayCallback";
  public void overlayScrollChanged(float progress) throws android.os.RemoteException;
  public void overlayStatusChanged(int status) throws android.os.RemoteException;
}
