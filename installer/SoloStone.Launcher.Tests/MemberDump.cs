using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

static partial class Program
{
    static void MemberDumpTests() {
        string dll=typeof(Program).Assembly.Location;
        string text=MemberDump.Describe(dll,"SoloStone.Minisign",new[]{"Verify"});
        Check(text.Contains("Verify("),"member dump lists the method signature");
        Check(text.Contains("IL "),"member dump gives the IL size of a named member");
        Check(text.Contains("calls:"),"member dump lists the calls of a named member");
        string names=MemberDump.Describe(dll,"SoloStone.Minisign*",new string[0]);
        // One bad body or list line must not discard the rest of the dump.
        using(var stream=File.OpenRead(dll)) using(var pe=new PEReader(stream)) {
            string bad=MemberDump.Summarize(pe.GetMetadataReader(),new byte[]{0xFE,0xFF,0x28,0x01});
            Check(bad.Contains("IL 4") && bad.Contains("(error reading IL:") && bad.Contains("calls:"),"an unreadable body prints an error line and keeps its summary");
        }
        string list=Path.GetTempFileName();
        try {
            File.WriteAllLines(list,new[]{"# c","First a","Broken","Last"});
            var written=new System.IO.StringWriter();
            MemberDump.Run(new[]{dll,list},written,(path,type,members)=>{ if(type=="Broken") throw new InvalidOperationException("boom"); return "ok "+type+"\n"; });
            Check(written.ToString()=="ok First\n(error in Broken: boom)\nok Last\n","a failing list line prints an error and the dump continues");
        } finally { File.Delete(list); }
        Check(names.Split('\n').Any(l=>l.Trim().StartsWith("SoloStone.Minisign",StringComparison.Ordinal)),"a pattern ending in * lists matching type names");
    }
}

// Prints what a client assembly declares, without loading it: for reading the Hearthstone client from the owner's PC.
//   tool members <assembly.dll> <list.txt>
// list.txt: one line per type, "TypeName [Member ...]". A type name ending in * lists the matching full type names only.
// '#' starts a comment. For each named member: IL size, the distinct members it calls, and the distinct int constants it loads.
static class MemberDump
{
    public static int Run(string[] args) => Run(args,Console.Out,Describe);

    // Each list line is written as soon as it is described, and one failing line prints an error line and the dump goes on.
    public static int Run(string[] args,TextWriter output,Func<string,string,IReadOnlyList<string>,string> describe) {
        if(args.Length!=2) { Console.Error.WriteLine("Usage: tool members ASSEMBLY.dll LIST.txt"); return 2; }
        foreach(string raw in File.ReadAllLines(args[1])) {
            string line=raw; int hash=line.IndexOf('#'); if(hash>=0) line=line.Substring(0,hash);
            var words=line.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
            if(words.Length==0) continue;
            string text;
            try { text=describe(args[0],words[0],words.Skip(1).ToArray()); }
            catch(Exception e) { text="(error in "+words[0]+": "+e.Message+")\n"; }
            output.Write(text); output.Flush();
        }
        return 0;
    }

    public static string Describe(string assemblyPath,string typePattern,IReadOnlyList<string> members) {
        using(var stream=File.OpenRead(assemblyPath))
        using(var pe=new System.Reflection.PortableExecutable.PEReader(stream)) {
            var md=pe.GetMetadataReader();
            var text=new System.Text.StringBuilder();
            var all=md.TypeDefinitions.Select(h=>(handle:h,name:BindingScan.TypeName(md,h))).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();
            bool wildcard=typePattern.EndsWith("*",StringComparison.Ordinal);
            string prefix=wildcard?typePattern.Substring(0,typePattern.Length-1):typePattern;
            var found=all.Where(t=>wildcard?t.name.StartsWith(prefix,StringComparison.Ordinal):t.name==prefix).ToArray();
            if(wildcard) { foreach(var t in found) text.Append(t.name).Append('\n'); if(found.Length==0) text.Append("(no type matches ").Append(typePattern).Append(")\n"); return text.ToString(); }
            if(found.Length==0) { text.Append("(no type ").Append(typePattern).Append(")\n"); return text.ToString(); }
            foreach(var t in found) DescribeType(md,pe,t.handle,t.name,members,text);
            return text.ToString();
        }
    }

    static void DescribeType(System.Reflection.Metadata.MetadataReader md,System.Reflection.PortableExecutable.PEReader pe,System.Reflection.Metadata.TypeDefinitionHandle handle,string name,IReadOnlyList<string> members,System.Text.StringBuilder text) {
        var type=md.GetTypeDefinition(handle);
        text.Append("type ").Append(name).Append('\n');
        foreach(var f in type.GetFields()) { var field=md.GetFieldDefinition(f); text.Append("  field ").Append(md.GetString(field.Name)).Append(" : ").Append(BindingScan.Signature(md,field)).Append('\n'); }
        foreach(var p in type.GetProperties()) {
            var property=md.GetPropertyDefinition(p); var sig=property.DecodeSignature(PropertyTypes,null);
            text.Append("  property ").Append(md.GetString(property.Name)).Append(" : ").Append(sig.ReturnType).Append('\n');
        }
        foreach(var m in type.GetMethods()) {
            var method=md.GetMethodDefinition(m); string methodName=md.GetString(method.Name);
            text.Append("  method ").Append(Spell(methodName,BindingScan.Signature(md,method))).Append('\n');
            if(!members.Contains(methodName)) continue;
            if(method.RelativeVirtualAddress==0) { text.Append("    IL 0 (no body)\n"); continue; }
            var body=pe.GetMethodBody(method.RelativeVirtualAddress);
            var il=body.GetILBytes();
            text.Append(Summarize(md,il));
        }
    }

    // IL size, calls and ints of one body. A body that cannot be read prints an error line instead of ending the dump.
    public static string Summarize(MetadataReader md,byte[] il) {
        var calls=new SortedSet<string>(StringComparer.Ordinal); var ints=new SortedSet<int>();
        var text=new System.Text.StringBuilder();
        text.Append("    IL ").Append(il.Length).Append('\n');
        try { Scan(md,il,calls,ints); }
        catch(Exception e) { text.Append("    (error reading IL: ").Append(e.Message).Append(")\n"); }
        text.Append("    calls: ").Append(string.Join("; ",calls)).Append('\n');
        text.Append("    ints: ").Append(string.Join(", ",ints)).Append('\n');
        return text.ToString();
    }

    // "ret(params)" in BindingScan's spelling becomes "Name(params) : ret".
    static string Spell(string name,string signature) { int open=signature.IndexOf('('); if(open<0) return name+" : "+signature; return name+signature.Substring(open)+" : "+signature.Substring(0,open); }

    sealed class PropertyProvider : System.Reflection.Metadata.ISignatureTypeProvider<string,object>
    {
        public string GetPrimitiveType(System.Reflection.Metadata.PrimitiveTypeCode code) => "System."+code;
        public string GetTypeFromDefinition(System.Reflection.Metadata.MetadataReader r,System.Reflection.Metadata.TypeDefinitionHandle h,byte kind) => BindingScan.TypeName(r,h);
        public string GetTypeFromReference(System.Reflection.Metadata.MetadataReader r,System.Reflection.Metadata.TypeReferenceHandle h,byte kind) => BindingScan.TypeName(r,h);
        public string GetTypeFromSpecification(System.Reflection.Metadata.MetadataReader r,object c,System.Reflection.Metadata.TypeSpecificationHandle h,byte kind) => r.GetTypeSpecification(h).DecodeSignature(this,c);
        public string GetSZArrayType(string e) => e+"[]";
        public string GetArrayType(string e,System.Reflection.Metadata.ArrayShape s) => e+"["+new string(',',s.Rank-1)+"]";
        public string GetByReferenceType(string e) => e+"&";
        public string GetPointerType(string e) => e+"*";
        public string GetPinnedType(string e) => e;
        public string GetGenericInstantiation(string g,System.Collections.Immutable.ImmutableArray<string> args) => g+"<"+string.Join(",",args)+">";
        public string GetGenericTypeParameter(object c,int i) => "!"+i;
        public string GetGenericMethodParameter(object c,int i) => "!!"+i;
        public string GetFunctionPointerType(System.Reflection.Metadata.MethodSignature<string> s) => "fn";
        public string GetModifiedType(string m,string t,bool required) => t;
    }
    static readonly PropertyProvider PropertyTypes=new PropertyProvider();

    static Dictionary<int,System.Reflection.Emit.OpCode> ops;
    static System.Reflection.Emit.OpCode Op(int value) {
        if(ops==null) ops=typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Select(f=>(System.Reflection.Emit.OpCode)f.GetValue(null)).ToDictionary(o=>(int)(ushort)o.Value);
        return ops[value];
    }

    static void Scan(System.Reflection.Metadata.MetadataReader md,byte[] il,SortedSet<string> calls,SortedSet<int> ints) {
        int at=0;
        Func<int> i32=()=>{ int v=BitConverter.ToInt32(il,at); at+=4; return v; };
        while(at<il.Length) {
            int first=il[at++];
            var op=Op(first==0xFE?0xFE00|il[at++]:first);
            string name=op.Name; int operand=0;
            switch(op.OperandType) {
                case System.Reflection.Emit.OperandType.InlineNone: break;
                case System.Reflection.Emit.OperandType.ShortInlineBrTarget: case System.Reflection.Emit.OperandType.ShortInlineI: case System.Reflection.Emit.OperandType.ShortInlineVar:
                    operand=name=="ldc.i4.s"?(sbyte)il[at]:il[at]; at+=1; break;
                case System.Reflection.Emit.OperandType.InlineVar: at+=2; break;
                case System.Reflection.Emit.OperandType.InlineI8: case System.Reflection.Emit.OperandType.InlineR: at+=8; break;
                case System.Reflection.Emit.OperandType.InlineSwitch: { int n=i32(); at+=4*n; break; }
                default: operand=i32(); break;
            }
            if(name=="call" || name=="callvirt" || name=="newobj") calls.Add(Callee(md,System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(operand)));
            else if(name=="ldc.i4") ints.Add(operand);
            else if(name=="ldc.i4.s") ints.Add(operand);
            else if(name=="ldc.i4.m1") ints.Add(-1);
            else if(name.StartsWith("ldc.i4.",StringComparison.Ordinal) && name.Length==8) ints.Add(name[7]-'0');
        }
    }

    static string Callee(System.Reflection.Metadata.MetadataReader md,System.Reflection.Metadata.EntityHandle target) {
        try {
            if(target.Kind==System.Reflection.Metadata.HandleKind.MethodSpecification) target=md.GetMethodSpecification((System.Reflection.Metadata.MethodSpecificationHandle)target).Method;
            if(target.Kind==System.Reflection.Metadata.HandleKind.MethodDefinition) {
                var def=md.GetMethodDefinition((System.Reflection.Metadata.MethodDefinitionHandle)target);
                return BindingScan.TypeName(md,def.GetDeclaringType())+"."+Spell(md.GetString(def.Name),BindingScan.Signature(md,def));
            }
            if(target.Kind==System.Reflection.Metadata.HandleKind.MemberReference) {
                var member=md.GetMemberReference((System.Reflection.Metadata.MemberReferenceHandle)target);
                string owner=Owner(md,member.Parent);
                var sig=member.DecodeMethodSignature(PropertyTypes,null);
                return owner+"."+md.GetString(member.Name)+"("+string.Join(",",sig.ParameterTypes)+") : "+sig.ReturnType;
            }
        } catch(Exception) { }
        return "?"+System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(target).ToString("X8");
    }

    static string Owner(System.Reflection.Metadata.MetadataReader md,System.Reflection.Metadata.EntityHandle parent) {
        switch(parent.Kind) {
            case System.Reflection.Metadata.HandleKind.TypeReference: return BindingScan.TypeName(md,(System.Reflection.Metadata.TypeReferenceHandle)parent);
            case System.Reflection.Metadata.HandleKind.TypeDefinition: return BindingScan.TypeName(md,(System.Reflection.Metadata.TypeDefinitionHandle)parent);
            case System.Reflection.Metadata.HandleKind.TypeSpecification: return md.GetTypeSpecification((System.Reflection.Metadata.TypeSpecificationHandle)parent).DecodeSignature(PropertyTypes,null);
            default: return "?";
        }
    }
}
