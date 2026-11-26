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

        // Strings, fields, delegates, compiler-generated bodies and the "!Name" listing, on this fixture.
        string fixture=MemberDump.Describe(dll,"MemberDumpFixture",new[]{"Lambda","Iterate","!Branch"});
        string Section(string method) { int at=fixture.IndexOf("  method "+method+"(",StringComparison.Ordinal); int end=fixture.IndexOf("\n  method ",at+1,StringComparison.Ordinal); return at<0?"":end<0?fixture.Substring(at):fixture.Substring(at,end-at); }
        string lambda=Section("Lambda"), iterate=Section("Iterate"), branch=Section("Branch");
        Check(lambda.Contains("strings: \"fixture-string\""),"member dump lists the strings a member loads");
        Check(lambda.Contains("fields: ") && lambda.Contains("MemberDumpFixture.Counter"),"member dump lists the fields a member reads or writes");
        Check(lambda.Contains("delegates: ") && lambda.Contains("<Lambda>b__"),"member dump lists the methods a member takes as delegates");
        Check(lambda.Contains("follows ") && lambda.Contains("\"inside-lambda\""),"member dump summarizes the lambda bodies a member reaches");
        Check(iterate.Contains("follows ") && iterate.Contains("MoveNext") && iterate.Contains("\"inside-iterator\""),"member dump follows an iterator into its MoveNext");
        Check(branch.Contains("listing:") && System.Text.RegularExpressions.Regex.IsMatch(branch,@"IL_[0-9A-F]{4}: br[a-z.]* IL_[0-9A-F]{4}") && branch.Contains("ldstr \"branch-yes\""),"a !Name member prints its listing with branch targets");
        Check(!Section("Untouched").Contains("IL "),"an unlisted member prints its signature only");
    }
}

// MemberDumpTests reads this type's bodies: a string, a static field, a lambda, an iterator and a branch.
static class MemberDumpFixture
{
    public static int Counter;
    public static Func<string> Lambda() { Counter++; string tag = "fixture-string"; return () => "inside-lambda" + tag.Length; }
    public static IEnumerable<string> Iterate() { yield return "inside-iterator"; }
    public static string Branch(bool yes) { if (yes) return "branch-yes"; return "branch-no"; }
    public static int Untouched() => 1;
}

// Prints what a client assembly declares, without loading it: for reading the Hearthstone client from the owner's PC.
//   tool members <assembly.dll> <list.txt>
// list.txt: one line per type, "TypeName [Member ...]". A type name ending in * lists the matching full type names only.
// '#' starts a comment. For each named member: IL size, the distinct members it calls, the int constants, strings and fields it
// loads, the methods it takes as delegates (ldftn), and the same summary for every compiler-generated method it reaches (lambdas,
// local functions, an iterator's MoveNext), two levels deep. A member written "!Name" also prints its full instruction listing
// with branch targets (for branch order, which the summary drops).
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
            bool listing=members.Contains("!"+methodName);
            if(!members.Contains(methodName) && !listing) continue;
            if(method.RelativeVirtualAddress==0) { text.Append("    IL 0 (no body)\n"); continue; }
            var body=pe.GetMethodBody(method.RelativeVirtualAddress);
            var il=body.GetILBytes();
            var follow=new List<MethodDefinitionHandle>();
            text.Append(Summarize(md,il,follow));
            if(listing) text.Append(Listing(md,il));
            Follow(md,pe,follow,new HashSet<MethodDefinitionHandle>{m},1,text);
        }
    }

    // What one body uses. A body that cannot be read prints an error line instead of ending the dump.
    sealed class Uses
    {
        public readonly SortedSet<string> Calls=new SortedSet<string>(StringComparer.Ordinal), Strings=new SortedSet<string>(StringComparer.Ordinal),
            Fields=new SortedSet<string>(StringComparer.Ordinal), Delegates=new SortedSet<string>(StringComparer.Ordinal);
        public readonly SortedSet<int> Ints=new SortedSet<int>();
        public readonly List<MethodDefinitionHandle> Follow=new List<MethodDefinitionHandle>();
    }

    // IL size, calls, ints, strings, fields and delegate targets of one body, indented by indent.
    public static string Summarize(MetadataReader md,byte[] il) => Summarize(md,il,null);
    static string Summarize(MetadataReader md,byte[] il,List<MethodDefinitionHandle> follow,string indent="    ") {
        var uses=new Uses();
        var text=new System.Text.StringBuilder();
        text.Append(indent).Append("IL ").Append(il.Length).Append('\n');
        try { Scan(md,il,uses); }
        catch(Exception e) { text.Append(indent).Append("(error reading IL: ").Append(e.Message).Append(")\n"); }
        text.Append(indent).Append("calls: ").Append(string.Join("; ",uses.Calls)).Append('\n');
        text.Append(indent).Append("ints: ").Append(string.Join(", ",uses.Ints)).Append('\n');
        if(uses.Strings.Count>0) text.Append(indent).Append("strings: ").Append(string.Join(" | ",uses.Strings)).Append('\n');
        if(uses.Fields.Count>0) text.Append(indent).Append("fields: ").Append(string.Join("; ",uses.Fields)).Append('\n');
        if(uses.Delegates.Count>0) text.Append(indent).Append("delegates: ").Append(string.Join("; ",uses.Delegates)).Append('\n');
        if(follow!=null) follow.AddRange(uses.Follow);
        return text.ToString();
    }

    // The compiler-generated methods a summary reached, each summarized under it, to depth 2.
    static void Follow(MetadataReader md,PEReader pe,List<MethodDefinitionHandle> targets,HashSet<MethodDefinitionHandle> seen,int depth,System.Text.StringBuilder text) {
        if(depth>2) return;
        string indent=new string(' ',4+2*depth);
        foreach(var h in targets) {
            if(!seen.Add(h)) continue;
            var def=md.GetMethodDefinition(h);
            text.Append(indent).Append("follows ").Append(BindingScan.TypeName(md,def.GetDeclaringType())).Append('.').Append(Spell(md.GetString(def.Name),BindingScan.Signature(md,def))).Append('\n');
            if(def.RelativeVirtualAddress==0) { text.Append(indent).Append("  IL 0 (no body)\n"); continue; }
            var next=new List<MethodDefinitionHandle>();
            try { text.Append(Summarize(md,pe.GetMethodBody(def.RelativeVirtualAddress).GetILBytes(),next,indent+"  ")); }
            catch(Exception e) { text.Append(indent).Append("  (error reading body: ").Append(e.Message).Append(")\n"); continue; }
            Follow(md,pe,next,seen,depth+1,text);
        }
    }

    // Compiler-generated: a name or declaring type with '<' (lambdas "<M>b__0", local functions, display classes, iterators).
    static bool Generated(MetadataReader md,MethodDefinition def) =>
        md.GetString(def.Name).IndexOf('<')>=0 || BindingScan.TypeName(md,def.GetDeclaringType()).IndexOf('<')>=0;

    // The method a call, newobj or ldftn token names in this assembly (a generic instantiation resolved), else null.
    static MethodDefinitionHandle? Defined(EntityHandle target) {
        if(target.Kind==HandleKind.MethodDefinition) return (MethodDefinitionHandle)target;
        return null;
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

    // One decoded instruction: offset, opcode, the operand token or value, the offset after it, and a switch's targets.
    struct Instruction { public int Offset, Operand, Next; public System.Reflection.Emit.OpCode Op; public int[] Targets; }

    static IEnumerable<Instruction> Decode(byte[] il) {
        int at=0;
        while(at<il.Length) {
            var ins=new Instruction { Offset=at };
            int first=il[at++];
            ins.Op=Op(first==0xFE?0xFE00|il[at++]:first);
            switch(ins.Op.OperandType) {
                case System.Reflection.Emit.OperandType.InlineNone: break;
                case System.Reflection.Emit.OperandType.ShortInlineBrTarget: ins.Operand=(sbyte)il[at]; at+=1; break;
                case System.Reflection.Emit.OperandType.ShortInlineI: case System.Reflection.Emit.OperandType.ShortInlineVar:
                    ins.Operand=ins.Op.Name=="ldc.i4.s"?(sbyte)il[at]:il[at]; at+=1; break;
                case System.Reflection.Emit.OperandType.InlineVar: ins.Operand=BitConverter.ToUInt16(il,at); at+=2; break;
                case System.Reflection.Emit.OperandType.InlineI8: case System.Reflection.Emit.OperandType.InlineR: at+=8; break;
                case System.Reflection.Emit.OperandType.InlineSwitch: {
                    int n=BitConverter.ToInt32(il,at); at+=4; ins.Targets=new int[n];
                    for(int i=0;i<n;i++) { ins.Targets[i]=BitConverter.ToInt32(il,at); at+=4; }
                    break;
                }
                default: ins.Operand=BitConverter.ToInt32(il,at); at+=4; break;
            }
            ins.Next=at;
            if(ins.Targets!=null) for(int i=0;i<ins.Targets.Length;i++) ins.Targets[i]+=at;
            yield return ins;
        }
    }

    static bool IsCall(string name) => name=="call" || name=="callvirt" || name=="newobj";
    static bool IsDelegate(string name) => name=="ldftn" || name=="ldvirtftn";
    static bool IsField(string name) => name=="ldfld" || name=="ldsfld" || name=="stfld" || name=="stsfld" || name=="ldflda" || name=="ldsflda";

    static void Scan(MetadataReader md,byte[] il,Uses uses) {
        foreach(var ins in Decode(il)) {
            string name=ins.Op.Name;
            if(IsCall(name) || IsDelegate(name)) {
                var target=Resolve(md,System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(ins.Operand));
                (IsCall(name)?uses.Calls:uses.Delegates).Add(Callee(md,target));
                var def=Defined(target);
                if(def!=null) {
                    var method=md.GetMethodDefinition(def.Value);
                    if(Generated(md,method)) uses.Follow.Add(def.Value);
                    // An iterator or async state machine: newobj of "<M>d__N" runs its MoveNext.
                    if(name=="newobj" && BindingScan.TypeName(md,method.GetDeclaringType()).IndexOf(">d__",StringComparison.Ordinal)>=0)
                        foreach(var mh in md.GetTypeDefinition(method.GetDeclaringType()).GetMethods())
                            if(md.GetString(md.GetMethodDefinition(mh).Name)=="MoveNext") uses.Follow.Add(mh);
                }
            }
            else if(IsField(name)) uses.Fields.Add(FieldName(md,System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(ins.Operand)));
            else if(name=="ldstr") uses.Strings.Add(Quote(md,ins.Operand));
            else if(name=="ldc.i4" || name=="ldc.i4.s") uses.Ints.Add(ins.Operand);
            else if(name=="ldc.i4.m1") uses.Ints.Add(-1);
            else if(name.StartsWith("ldc.i4.",StringComparison.Ordinal) && name.Length==8) uses.Ints.Add(name[7]-'0');
        }
    }

    // "!Name": every instruction, "IL_0012: brfalse.s IL_0030", with call targets, fields, strings and ints spelled out.
    static string Listing(MetadataReader md,byte[] il) {
        var text=new System.Text.StringBuilder();
        text.Append("    listing:\n");
        try {
            foreach(var ins in Decode(il)) {
                string name=ins.Op.Name, operand="";
                var type=ins.Op.OperandType;
                if(IsCall(name) || IsDelegate(name)) operand=Callee(md,Resolve(md,System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(ins.Operand)));
                else if(IsField(name)) operand=FieldName(md,System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(ins.Operand));
                else if(name=="ldstr") operand=Quote(md,ins.Operand);
                else if(type==System.Reflection.Emit.OperandType.ShortInlineBrTarget || type==System.Reflection.Emit.OperandType.InlineBrTarget) operand=Label(ins.Next+ins.Operand);
                else if(ins.Targets!=null) operand="("+string.Join(", ",ins.Targets.Select(Label))+")";
                else if(type==System.Reflection.Emit.OperandType.InlineTok || type==System.Reflection.Emit.OperandType.InlineType) operand=TokenName(md,ins.Operand);
                else if(type!=System.Reflection.Emit.OperandType.InlineNone && type!=System.Reflection.Emit.OperandType.InlineI8 && type!=System.Reflection.Emit.OperandType.InlineR) operand=ins.Operand.ToString();
                text.Append("      ").Append(Label(ins.Offset)).Append(": ").Append(name);
                if(operand.Length>0) text.Append(' ').Append(operand);
                text.Append('\n');
            }
        } catch(Exception e) { text.Append("      (error reading IL: ").Append(e.Message).Append(")\n"); }
        return text.ToString();
    }

    static string Label(int offset) => "IL_"+offset.ToString("X4");

    static EntityHandle Resolve(MetadataReader md,EntityHandle target) =>
        target.Kind==HandleKind.MethodSpecification ? md.GetMethodSpecification((MethodSpecificationHandle)target).Method : target;

    // A ldstr literal, quoted, one line, at most 120 characters.
    static string Quote(MetadataReader md,int token) {
        try {
            string value=md.GetUserString((UserStringHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.Handle(token));
            value=value.Replace("\\","\\\\").Replace("\r","\\r").Replace("\n","\\n").Replace("\"","\\\"");
            if(value.Length>120) value=value.Substring(0,120)+"...";
            return "\""+value+"\"";
        } catch(Exception) { return "?str"+token.ToString("X8"); }
    }

    // "Owner.field" for a field definition or reference.
    static string FieldName(MetadataReader md,EntityHandle target) {
        try {
            if(target.Kind==HandleKind.FieldDefinition) {
                var f=md.GetFieldDefinition((FieldDefinitionHandle)target);
                return BindingScan.TypeName(md,f.GetDeclaringType())+"."+md.GetString(f.Name);
            }
            if(target.Kind==HandleKind.MemberReference) {
                var member=md.GetMemberReference((MemberReferenceHandle)target);
                return Owner(md,member.Parent)+"."+md.GetString(member.Name);
            }
        } catch(Exception) { }
        return "?"+System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(target).ToString("X8");
    }

    // A ldtoken / type operand: the type, method or field it names.
    static string TokenName(MetadataReader md,int token) {
        try {
            var h=System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);
            switch(h.Kind) {
                case HandleKind.TypeDefinition: case HandleKind.TypeReference: case HandleKind.TypeSpecification: return Owner(md,h);
                case HandleKind.FieldDefinition: return FieldName(md,h);
                case HandleKind.MemberReference:
                    return md.GetMemberReference((MemberReferenceHandle)h).GetKind()==MemberReferenceKind.Field ? FieldName(md,h) : Callee(md,h);
                default: return Callee(md,Resolve(md,h));
            }
        } catch(Exception) { return "?"+token.ToString("X8"); }
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
