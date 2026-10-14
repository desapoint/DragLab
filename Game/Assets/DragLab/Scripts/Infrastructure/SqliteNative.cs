using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace DragLab.Infrastructure
{
    internal sealed class SqliteNative : IDisposable
    {
        const string Library="draglab_sqlite3";
        IntPtr handle;
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] path,out IntPtr db,int flags,IntPtr vfs);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_busy_timeout(IntPtr db,int ms);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_exec(IntPtr db,byte[] sql,IntPtr callback,IntPtr arg,IntPtr error);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int length,out IntPtr stmt,IntPtr tail);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_reset(IntPtr stmt);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_clear_bindings(IntPtr stmt);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_text(IntPtr stmt,int index,byte[] value,int length,IntPtr destroy);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_int64(IntPtr stmt,int index,long value);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_null(IntPtr stmt,int index);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr stmt);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr stmt,int index);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr stmt,int index);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_backup_init(IntPtr destination,byte[] destName,IntPtr source,byte[] sourceName);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_backup_step(IntPtr backup,int pages);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_backup_finish(IntPtr backup);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_libversion();
        static byte[] Utf8(string text)=>Encoding.UTF8.GetBytes(text+"\0");
        static string String(IntPtr pointer,int length=-1){if(pointer==IntPtr.Zero)return null;if(length<0){length=0;while(Marshal.ReadByte(pointer,length)!=0)length++;}var data=new byte[length];Marshal.Copy(pointer,data,0,length);return Encoding.UTF8.GetString(data);}
        public static string Version=>String(sqlite3_libversion());
        public SqliteNative(string path,bool readOnly=false)
        {
            int result=sqlite3_open_v2(Utf8(path),out handle,readOnly?1|0x10000:2|4|0x10000,IntPtr.Zero);
            if(result!=0){string error=String(sqlite3_errmsg(handle));Dispose();throw new IOException("SQLite open: "+error);}
            sqlite3_busy_timeout(handle,1500);Exec("PRAGMA foreign_keys=ON; PRAGMA trusted_schema=OFF;");
            if(!readOnly)Exec("PRAGMA synchronous=FULL;");
        }
        void Check(int code){if(code!=0)throw new IOException("SQLite "+code+": "+String(sqlite3_errmsg(handle)));}
        public void Exec(string sql)=>Check(sqlite3_exec(handle,Utf8(sql),IntPtr.Zero,IntPtr.Zero,IntPtr.Zero));
        public Statement Prepare(string sql)=>new Statement(this,sql);
        public List<string[]> Query(string sql,params object[] values){using(var statement=Prepare(sql)){statement.Bind(values);return statement.Rows();}}
        public string Scalar(string sql,params object[] values){var rows=Query(sql,values);return rows.Count==0?null:rows[0][0];}
        public void Backup(string path)
        {
            using(var dest=new SqliteNative(path)){var backup=sqlite3_backup_init(dest.handle,Utf8("main"),handle,Utf8("main"));if(backup==IntPtr.Zero)throw new IOException("SQLite backup initialization failed.");int result;try{result=sqlite3_backup_step(backup,-1);}finally{Check(sqlite3_backup_finish(backup));}if(result!=101)throw new IOException("SQLite backup incomplete: "+result);}
        }
        public void Dispose(){if(handle!=IntPtr.Zero){sqlite3_close_v2(handle);handle=IntPtr.Zero;}}
        internal sealed class Statement : IDisposable
        {
            readonly SqliteNative owner;IntPtr stmt;
            public Statement(SqliteNative owner,string sql){this.owner=owner;byte[] data=Utf8(sql);owner.Check(sqlite3_prepare_v2(owner.handle,data,data.Length-1,out stmt,IntPtr.Zero));}
            public void Bind(params object[] values){sqlite3_reset(stmt);owner.Check(sqlite3_clear_bindings(stmt));for(int i=0;i<values.Length;i++){var value=values[i];if(value==null)owner.Check(sqlite3_bind_null(stmt,i+1));else if(value is string text){byte[] data=Utf8(text);owner.Check(sqlite3_bind_text(stmt,i+1,data,data.Length-1,new IntPtr(-1)));}else owner.Check(sqlite3_bind_int64(stmt,i+1,Convert.ToInt64(value,System.Globalization.CultureInfo.InvariantCulture)));}}
            public void Execute(params object[] values){Bind(values);int code=sqlite3_step(stmt);if(code!=101)owner.Check(code);}
            public List<string[]> Rows(){var rows=new List<string[]>();int code;while((code=sqlite3_step(stmt))==100){var row=new string[sqlite3_column_count(stmt)];for(int i=0;i<row.Length;i++)row[i]=String(sqlite3_column_text(stmt,i),sqlite3_column_bytes(stmt,i));rows.Add(row);}if(code!=101)owner.Check(code);return rows;}
            public void Dispose(){if(stmt!=IntPtr.Zero){sqlite3_finalize(stmt);stmt=IntPtr.Zero;}}
        }
    }
}
