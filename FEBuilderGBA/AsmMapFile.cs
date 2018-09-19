﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;

namespace FEBuilderGBA
{
    public class AsmMapFile
    {
        public AsmMapFile()
        {
            string asmmap;
            asmmap = U.ConfigDataFilename("asmmap_addition_");
            if (File.Exists(asmmap))
            {//追加マップファイル
                Log.Notify("Load asmmap_addition", asmmap);
                //型情報のスキャン.
                ASMMapLoadStruct(asmmap);
                //逆アセンブラ用 map
                ASMMapLoadResource(asmmap);
            }
            //ROM情報をmapとして利用する.
            ROMInfoLoadResource();

            asmmap = U.ConfigDataFilename("asmmap_");
            //型情報のスキャン.
            ASMMapLoadStruct(asmmap);
            //構造体スキャン
            ROMTypeLoadResource();
           
            //逆アセンブラ用 map
            ASMMapLoadResource(asmmap);
            //gba共通データ
            ASMMapLoadResource(U.ConfigDataFilename("asmmap_gba_"));
        }


        public AsmMapFile(AsmMapFile orignal)
        {
            this.NearSearchSortedList = new List<uint>(orignal.NearSearchSortedList);
            this.AsmStructs = new Dictionary<string, AsmStruct>(orignal.AsmStructs);
            this.AsmMap = new Dictionary<uint, AsmMapSt>(orignal.AsmMap);
        }


        //Formで作っていた型情報を追加.
        void ROMTypeLoadResource()
        {
            char[] skipTrim = new char[] { ' ', '_' };
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (Type q in assembly.GetTypes())
            {
                if (!q.IsClass)
                {
                    continue;
                }
                if (q.Name.LastIndexOf("Form") == q.Name.Length - 4)
                {
                    continue;
                }

                List<AsmStructNode> structSt = new List<AsmStructNode>();
                const BindingFlags FINDS_FLAG = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                FieldInfo[] fields = q.GetFields(FINDS_FLAG);
                foreach(FieldInfo f in fields)
                {
                    string name = f.Name;
                    if (name.IndexOf("L_") >= 0 || name.IndexOf("J_") >= 0)
                    {
                        continue;
                    }

                    string prefix = InputFormRef.GetPrefixName(name);
                    uint id = InputFormRef.GetStructID(prefix, name);
                    if (id == U.NOT_FOUND)
                    {
                        continue;
                    }

                    AsmStruct aStruct;
                    string key = q.Name + prefix;
                    if (AsmStructs.ContainsKey(key))
                    {
                        aStruct = AsmStructs[key];
                    }
                    else
                    {
                        aStruct = new AsmStruct();
                        aStruct.Nodes = new List<AsmStructNode>();
                        AsmStructs[key] = aStruct;
                    }

                    AsmStructNode node;
                    string a = InputFormRef.SkipPrefixName(name,prefix);
                    if (a.Length <= 0)
                    {
                        continue;
                    }
                    else if (a[0] == 'b')
                    {
                        node = new AsmStructNode();
                        node.Type = AsmStructTypeEnum.b;
                        aStruct.SizeOf = Math.Max(aStruct.SizeOf, id + 1);
                    }
                    else if (a[0] == 'B')
                    {
                        node = new AsmStructNode();
                        node.Type = AsmStructTypeEnum.B;
                        aStruct.SizeOf = Math.Max(aStruct.SizeOf, id + 1);
                    }
                    else if (a[0] == 'W')
                    {
                        node = new AsmStructNode();
                        node.Type = AsmStructTypeEnum.W;
                        aStruct.SizeOf = Math.Max(aStruct.SizeOf, id + 2);
                    }
                    else if (a[0] == 'D')
                    {
                        node = new AsmStructNode();
                        node.Type = AsmStructTypeEnum.D;
                        aStruct.SizeOf = Math.Max(aStruct.SizeOf, id + 4);
                    }
                    else if (a[0] == 'P')
                    {
                        node = new AsmStructNode();
                        node.Type = AsmStructTypeEnum.P;
                        aStruct.SizeOf = Math.Max(aStruct.SizeOf, id + 4);
                    }
                    else
                    {//不明
                        continue;
                    }
                    node.Offset = id;
                    node.Name = "";
                    node.Comment = "";
                    node.TypeName = "";

                    string find_jump_name = "J_" + id ;
                    string find_label_name = "L_" + id ;
                    foreach (FieldInfo ff in fields)
                    {
                        int p = ff.Name.IndexOf(find_jump_name);
                        if (p >= 0)
                        {
                            uint lid = InputFormRef.GetStructID(prefix, name);
                            if (lid != id)
                            {
                                continue;
                            }
                            node.Name = U.substr(ff.Name, p + find_jump_name.Length + 1).Trim(skipTrim);
                            if (node.TypeName.Length < 0)
                            {
                                node.TypeName = node.Name;
                            }
                            continue;
                        }
                        p = ff.Name.IndexOf(find_label_name);
                        if (p >= 0)
                        {
                            uint lid = InputFormRef.GetStructID(prefix, name);
                            if (lid != id)
                            {
                                continue;
                            }
                            string label = U.substr(ff.Name, p + find_label_name.Length + 1).Trim(skipTrim);
                            if (label == "COMBO")
                            {
                                continue;
                            }
                            node.TypeName = label;
                            continue;
                        }
                    }
                    aStruct.Nodes.Add(node);
                }
            }
        }


        void ROMInfoLoadResource()
        {
            //せっかくなので、ROMで判明しているデータも追加する.
            MethodInfo[] methods = Program.ROM.RomInfo.GetType().GetMethods();
            foreach (MethodInfo info in methods)
            {
                if (info.Name.IndexOf("_pointer") >= 0)
                {
                    uint addr = (uint)info.Invoke(Program.ROM.RomInfo, null);
                    addr = U.toOffset(addr);
                    if (addr > 0 && U.isSafetyOffset(addr))
                    {
                        uint pointer = U.toPointer(addr);

                        AsmMapSt p = new AsmMapSt();
                        p.Name = info.Name;
                        AsmMap[pointer] = p;

                        uint pointer2 = Program.ROM.u32(addr);
                        if (U.isPointer(pointer2))
                        {
                            p = new AsmMapSt();
                            p.Name = info.Name.Replace("_pointer", "_address");
                            AsmMap[pointer2] = p;
                        }
                    }
                }

                if (info.Name.IndexOf("_address") >= 0)
                {
                    uint addr = (uint)info.Invoke(Program.ROM.RomInfo, null);
                    addr = U.toOffset(addr);
                    if (addr > 0 && U.isSafetyOffset(addr))
                    {
                        uint pointer = U.toPointer(addr);
                        AsmMapSt p = new AsmMapSt();
                        p.Name = info.Name;
                        AsmMap[pointer] = p;
                    }
                }
            }
        }

        public class AsmMapSt
        {
            //public uint Pointer;  keyに移動
            public string Name = "";
            public string ResultAndArgs = "";
            public string TypeName = "";
            public uint Length;

            public string ToStringInfo()
            {
                if (ResultAndArgs != "")
                {
                    return Name + " " + ResultAndArgs;
                }
                return Name;
            }
        };
        Dictionary<uint, AsmMapSt> AsmMap = new Dictionary<uint, AsmMapSt>();

        public Dictionary<uint, AsmMapSt> GetAsmMap()
        {
            return this.AsmMap;
        }

        enum AsmStructTypeEnum
        {
	         P
	        ,B
	        ,W
	        ,D
	        ,b
        };
        class AsmStructNode
        {
	        public	uint	Offset;
	        public	AsmStructTypeEnum	Type;
	        public	string	Name = "";
	        public	string	Comment = "";
            public  string  TypeName = "";
        };
        class AsmStruct
        {
        //	public	string	Name;
	        public	uint	SizeOf;
	        public	List<AsmStructNode>	Nodes;
        };

        void ASMMapLoadStruct(string fullfilename)
        {
            //ユーザが定義したmapファイル
            if (!File.Exists(fullfilename))
            {
                Debug.Assert(false);
                return;
            }
            using (StreamReader reader = File.OpenText(fullfilename))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (U.IsComment(line) || U.OtherLangLine(line))
                    {
                        continue;
                    }
                    line = U.ClipComment(line);

                    string[] sp = line.Split('\t');
                    if (sp.Length <= 2)
                    {
                        continue;
                    }

                    string op = sp[0];
                    if (op.Length <= 0)
                    {
                        continue;
                    }

                    if (op[0] != '@')
                    {//struct定義でなければ無視
                        continue;
                    }

                    string[] st = op.Split('@');
                    if (st.Length <= 2)
                    {
                        continue;
                    }
                    AsmStruct asmStrict;
                    if (!AsmStructs.ContainsKey(st[1]))
                    {
                        asmStrict = new AsmStruct();
                        asmStrict.SizeOf = 0;
                        asmStrict.Nodes = new List<AsmStructNode>();
                        AsmStructs[st[1]] = asmStrict;
                    }
                    else
                    {
                        asmStrict = AsmStructs[st[1]];
                    }
                    AsmStructNode node = new AsmStructNode();
                    node.Offset = U.atoh(st[2]);

                    string type = sp[1];
                    if (type == "short")
                    {
                        asmStrict.SizeOf = Math.Max(asmStrict.SizeOf, node.Offset + 2);
                        node.Type = AsmStructTypeEnum.W;
                    }
                    else if (type == "sbyte")
                    {
                        asmStrict.SizeOf = Math.Max(asmStrict.SizeOf, node.Offset + 1);
                        node.Type = AsmStructTypeEnum.b;
                    }
                    else if (type == "word" || type == "dword")
                    {
                        asmStrict.SizeOf = Math.Max(asmStrict.SizeOf, node.Offset + 4);
                        node.Type = AsmStructTypeEnum.D;
                    }
                    else if (type == "pointer")
                    {
                        asmStrict.SizeOf = Math.Max(asmStrict.SizeOf, node.Offset + 4);
                        node.Type = AsmStructTypeEnum.P;
                    }
                    else if (type.Length > 0 && type[0] == '&')
                    {
                        asmStrict.SizeOf = Math.Max(asmStrict.SizeOf, node.Offset + 4);
                        node.Type = AsmStructTypeEnum.P;
                        node.TypeName = type.Substring(1);
                    }
                    else
                    {
                        asmStrict.SizeOf = Math.Max(asmStrict.SizeOf, node.Offset + 1);
                        node.Type = AsmStructTypeEnum.B; //不明
                    }

                    node.Name = sp[2];
                    StringBuilder comment = new StringBuilder();
                    for (int i = 3; i < sp.Length; i++)
                    {
                        comment.Append(" ");
                        comment.Append(sp[i]);
                    }
                    node.Comment = U.substr( comment.ToString() , 1);

                    asmStrict.Nodes.Add(node);
                }
            }
        }

        Dictionary<string, AsmStruct> AsmStructs = new Dictionary<string, AsmStruct>();

        void ASMMapLoadResource(string fullfilename)
        {
            if (!U.IsRequiredFileExist(fullfilename))
            {
                return;
            }

            //ユーザが定義したmapファイル
            using (StreamReader reader = File.OpenText(fullfilename))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (U.IsComment(line) || U.OtherLangLine(line))
                    {
                        continue;
                    }
                    line = U.ClipComment(line);

                    string[] sp = line.Split('\t');
                    if (sp.Length <= 1)
                    {
                        continue;
                    }

                    string op = sp[0];
                    if (op.Length <= 0)
                    {
                        continue;
                    }

                    if ( op[0] == '@')
                    {//struct定義
                        continue;
                    }

                    uint pointer = U.toPointer(U.atoh(op));
                    if (pointer < 0x02000000)
                    {
                        continue;
                    }

                    if (sp[1].Length <= 0)
                    {
                        continue;
                    }
                    if (sp[1][0] == '@')
                    {//structを利用している
                        if (sp.Length <= 2)
                        {
                            continue;
                        }
                        string[] types = sp[1].Split('@');
                        if (types.Length <= 1)
                        {
                            continue;
                        }
                        //配列指定. Cとは違い型の方につけます。 @STRUCT@[A] 値は16進数です. 
                        uint count = 0;
                        if (types.Length >= 3)
                        {
                            if (types[2].Length <= 0 && types[2][0] != '[')
                            {
                                Debug.Assert(false);
                                continue;
                            }
                            count = U.atoh(U.substr(types[2], 1));
                            if (count <= 0)
                            {
                                Debug.Assert(false);
                            }
                        }

                        string structName = types[1];
                        if (!AsmStructs.ContainsKey(structName))
                        {
                            Debug.Assert(false);
                            continue;
                        }
                        AsmStruct asmSt = AsmStructs[structName];
                        if (count <= 1)
                        {
                            for (int i = 0; i < asmSt.Nodes.Count; i++)
                            {
                                AsmStructNode node = asmSt.Nodes[i];
                                AsmMapSt p = new AsmMapSt();
                                //p.Pointer = pointer; //keyに移動.
                                p.Name = structName + "@" + sp[2] + "." + node.Name;
                                if (node.TypeName == "")
                                {
                                    p.ResultAndArgs = node.Comment;
                                    p.TypeName = "";
                                }
                                else
                                {
                                    p.ResultAndArgs = "RET=@" + node.TypeName + " " + node.Comment;
                                    p.TypeName = node.TypeName;
                                }
                                AsmMap[pointer + node.Offset] = p;
                            }
                        }
                        else
                        {
                            for (uint n = 0; n < count; n++)
                            {
                                for (int i = 0; i < asmSt.Nodes.Count; i++)
                                {
                                    AsmStructNode node = asmSt.Nodes[i];
                                    AsmMapSt p = new AsmMapSt();
                                    //p.Pointer = pointer; //keyに移動.
                                    p.Name = structName + "@" + sp[2] + "[" + n + "]" + "." + node.Name;
                                    p.ResultAndArgs = node.Comment;
                                    p.TypeName = node.TypeName;

                                    AsmMap[pointer + node.Offset] = p;
                                }
                                pointer += asmSt.SizeOf;
                            }
                        }
                    }
                    else if (sp[1][0] == ':')
                    {//範囲指定 絶対指定
                        if (sp.Length <= 2)
                        {
                            continue;
                        }
                        uint endpointer = U.toPointer(U.atoh( sp[1].Substring(1) ));
                        if (endpointer == 0)
                        {
                            Debug.Assert(false);
                            endpointer = pointer;
                        }
                        else if (pointer > endpointer)
                        {
                            Debug.Assert(false);
                            U.Swap(ref pointer, ref endpointer);
                        }

                        int i = 2;

                        AsmMapSt p = new AsmMapSt();
                        //p.Pointer = pointer; //keyに移動.

                        if (sp[i].Length > 0 && sp[i][0] == '&')
                        {
                            p.TypeName = sp[i].Substring(1);
                            i++;
                        }

                        p.Name = sp[i];
                        i++;

                        StringBuilder comment = new StringBuilder();
                        for (; i < sp.Length; i++)
                        {
                            comment.Append(" ");
                            comment.Append(sp[i]);
                        }
                        p.Length = endpointer - pointer;
                        p.ResultAndArgs = U.substr(comment.ToString(), 1);
                        p.TypeName = "";

                        AsmMap[pointer] = p;
                    }
                    else
                    {//structを利用していない 範囲してもしない
                        int i = 1;
                        AsmMapSt p = new AsmMapSt();
                        //p.Pointer = pointer; //keyに移動.

                        string type = "";
                        if (sp[i].Length > 0 && sp[i][0] == '&')
                        {
                            type = sp[i].Substring(1);
                            i++;
                        }

                        p.Name = U.at(sp,i);
                        i++;

                        StringBuilder comment = new StringBuilder();
                        for (; i < sp.Length; i++)
                        {
                            comment.Append(" ");
                            comment.Append(sp[i]);
                            
                            int start = sp[i].IndexOf("RET=");
                            if (start >= 0)
                            {
                                start += 4;
                                int term = sp[i].IndexOf(" ",start);
                                if (term < 0)
                                {
                                    type = U.substr(sp[i], start);
                                }
                                else
                                {
                                    type = U.substr(sp[i], start, term - start);
                                }
                            }
                        }
                        p.ResultAndArgs = U.substr(comment.ToString(), 1);
                        p.TypeName = type;

                        TypeToLengthAndName(p, type, pointer);

                        AsmMap[pointer] = p;
                    }

                }
            }
        }

        void TypeToLengthAndName(AsmMapSt p,string type,uint pointer)
        {
            if (type == "LZ77")
            {
                p.Length = LZ77.getCompressedSize(Program.ROM.Data, U.toOffset(pointer));
            }
            else if (type == "OAMREGS")
            {
                p.Length = U.OAMREGSLength(U.toOffset(pointer));
                p.Name += " Count_" + ((p.Length - 2) / (3 * 2));
            }
            else if (type == "TEXTBATCH")
            {
                p.Length = U.TextBatchLength(U.toOffset(pointer));
                p.Name += " Count_" + ((p.Length) / 8);
            }
            else if (type == "EVENT")
            {
                p.Length = EventScript.SearchEveneLength(Program.ROM.Data, U.toOffset(pointer));
                p.Name += " Count_" + ((p.Length - 2) / (3 * 2));
            }
            else if (type == "HEADERTSA")
            {
                p.Length = ImageUtil.CalcByteLengthForHeaderTSAData(Program.ROM.Data, (int)U.toOffset(pointer));
            }
            else if (type == "PALETTE")
            {
                p.Length = 0x20;
            }
            else if (type == "PALETTE2")
            {
                p.Length = 0x20 * 2;
            }
        }
        public void AppendMAP(List<Address> list,string typeName = "")
        {
            for(int i = 0 ; i < list.Count ; i++)
            {
                uint pointer = list[i].Addr;
                if (pointer == U.NOT_FOUND || pointer <= 1)
                {
                    continue;
                }
                pointer = U.toPointer(pointer);
                if (!U.isSafetyPointer(pointer))
                {
                    continue;
                }
                AsmMapSt a;
                if (AsmMap.TryGetValue(pointer,out a))
                {//既に知っている
                    if (a.Length >= list[i].Length)
                    {//既に知っているデータより長いデータではないので無視する
                        continue;
                    }
                    if (a.TypeName.Length > 0 && typeName.Length <= 0)
                    {//相手側にtypeが指定されており、現在追加するのはタイプが指定されていない
                        continue;
                    }
                }
                AsmMapSt p = new AsmMapSt();
                p.Name = list[i].Info;
                p.ResultAndArgs = "";
                p.Length = list[i].Length;
                p.TypeName = typeName;

                if (p.Name == "")
                {
                    p.Name = "_" + U.ToHexString(pointer);
                }
                AsmMap[pointer] = p;
            }

            for (int i = 0; i < list.Count; i++)
            {
                uint pointer = list[i].Pointer;
                if (pointer == U.NOT_FOUND || pointer <= 1)
                {
                    continue;
                }
                pointer = U.toPointer(pointer);
                if (!U.isSafetyPointer(pointer))
                {
                    continue;
                }
                if (AsmMap.ContainsKey(pointer))
                {
                    continue;
                }
                AsmMapSt p = new AsmMapSt();
                p.Name = list[i].Info;
                p.ResultAndArgs = "";
                p.Length = 0;
                p.TypeName = typeName;

                if (p.Name == "")
                {
                    p.Name = "_" + U.ToHexString(pointer);
                }
                AsmMap[pointer] = p;
            }
        }


        public void AppendMAP(Dictionary<uint, uint> dic)
        {
            foreach(var pair in dic)
            {
                {
                    uint length = 4;
                    uint pointer = pair.Key;
                    pointer = U.toPointer(pointer);
                    if (!U.isSafetyPointer(pointer))
                    {
                        continue;
                    }
                    AsmMapSt a;
                    if (AsmMap.TryGetValue(pointer, out a))
                    {//既に知っている
                        if (a.Length >= length)
                        {//既に知っているデータより長いデータではないので無視する
                            continue;
                        }
                        if (a.TypeName.Length > 0 )
                        {//相手側にtypeが指定されており、現在追加するのはタイプが指定されていない
                            continue;
                        }
                    }
                    AsmMapSt p = new AsmMapSt();
                    p.Name = R._("SWITCH CASE");
                    p.ResultAndArgs = R._("とび先のコード:{0}", U.ToHexString(pointer));
                    p.Length = length;

                    AsmMap[pointer] = p;
                }
            }
        }

        public bool ContainsKey(uint pointer)
        {
            return this.AsmMap.ContainsKey(pointer);
        }

        public bool TryGetValue(uint pointer, out AsmMapFile.AsmMapSt out_p)
        {
            return this.AsmMap.TryGetValue(pointer,out out_p);
        }
        public string GetInfoFull(uint pointer)
        {
            AsmMapFile.AsmMapSt p ;
            if (!TryGetValue(pointer, out p))
            {
                return "";
            }
            return p.ToStringInfo();
        }


        //長さがあるROMのデータだけ取得.なおポインタはすべて不明となる.
        public void MakeAllDataLength(List<Address> list)
        {
            foreach (var pair in this.AsmMap)
            {
                if (pair.Value.Length <= 0)
                {
                    continue;
                }
                if (!U.isPointer(pair.Key))
                {
                    continue;
                }
                FEBuilderGBA.Address.AddAddress(list, U.toOffset(pair.Key),pair.Value.Length , U.NOT_FOUND, pair.Value.Name + "\t"+pair.Value.ResultAndArgs , Address.DataTypeEnum.POINTER );
            }
        }

        public static string GetSWI_GBA_BIOS_CALL(uint swicode)
        {
//swi GBA BIOS割り込みコード表
            switch (swicode)
            {
                case 0x00: return "SoftReset";
                case 0x01: return "RegisterRamReset";
                case 0x02: return "Halt";
                case 0x03: return "Stop";
                case 0x04: return "IntrWait";
                case 0x05: return "VBlankIntrWait";
                case 0x06: return "Div";
                case 0x07: return "DivArm";
                case 0x08: return "Sqrt";
                case 0x09: return "ArcTan";
                case 0x0A: return "ArcTan2";
                case 0x0B: return "CpuSet";
                case 0x0C: return "CpuFastSet";
                case 0x0D: return "GetBiosCheckSum";
                case 0x0E: return "BgAffineSet";
                case 0x0F: return "ObjAffineSet";
                case 0x10: return "BitUnPack";
                case 0x11: return "LZ77UnCompNormalWrite8bit";
                case 0x12: return "LZ77UnCompNormalWrite8bit";
                case 0x13: return "HuffUnCompReadNormal";
                case 0x14: return "RLUnCompReadNormalWrite8bit";
                case 0x15: return "RLUnCompReadNormalWrite16bit";
                case 0x16: return "Diff8bitUnFilterNormalWrite8bit";
                case 0x17: return "Diff8bitUnFilterNormalWrite8bit";
                case 0x18: return "Diff16bitUnFilter";
                case 0x19: return "SoundBias";
                case 0x1A: return "SoundDriverInit";
                case 0x1B: return "SoundDriverMode";
                case 0x1C: return "SoundDriverMain";
                case 0x1D: return "SoundDriverVSync";
                case 0x1E: return "SoundChannelClear";
                case 0x1F: return "MidiKey2Freq";
                case 0x20: return "SoundWHatever0";
                case 0x21: return "SoundWHatever1";
                case 0x22: return "SoundWHatever2";
                case 0x23: return "SoundWHatever3";
                case 0x24: return "SoundWHatever4";
                case 0x25: return "MultiBoot";
                case 0x26: return "HardReset";
                case 0x27: return "CustomHalt";
                case 0x28: return "SoundDriverVSyncOff";
                case 0x29: return "SoundDriverVSyncOn";
                case 0x2A: return "SoundGetJumpList";
            }
            return "";
        }

        //switch文と思われるところをマークする
        public static void MakeSwitchDataList(Dictionary<uint, uint> list, uint addr, uint end)
        {
            {
                byte[] mov_PC_r0 = new byte[] { 0x87, 0x46 };
                MakeSwitchDataListInner(list, addr, end, mov_PC_r0);
            }
            {
                byte[] mov_PC_r1 = new byte[] { 0x8F, 0x46 };
                MakeSwitchDataListInner(list, addr, end, mov_PC_r1);
            }
            {
                byte[] mov_PC_r2 = new byte[] { 0x97, 0x46 };
                MakeSwitchDataListInner(list, addr, end, mov_PC_r2);
            }
        }

        //switch文と思われるところをマークする
        static void MakeSwitchDataListInner(Dictionary<uint, uint> list, uint addr, uint end, byte[] mov_PC_r0)
        {
            Debug.Assert(mov_PC_r0.Length == 2);

            if (end <= 0)
            {
                end = (uint)Program.ROM.Data.Length;
            }

            while (addr < end)
            {
                addr = U.Grep(Program.ROM.Data, mov_PC_r0, addr, 0, 2);
                if (addr == U.NOT_FOUND)
                {
                    return;
                }
                uint mov_PC_pointer = U.toPointer(addr);
                addr += 2;
                if (addr % 4 != 0)
                {//4で割り切れない場合パディングしないとダメ.
                    addr += 2;
                }
                //だいたいテーブルへのポインタがある.
                uint checkEnd = addr + (4 * 4);
                for (; addr < checkEnd; addr += 4)
                {
                    if (addr >= end)
                    {
                        break;
                    }

                    uint p = Program.ROM.u32(addr);
                    if (!U.isSafetyPointer(p))
                    {
                        continue;
                    }
                    uint this_pointer = U.toPointer(p);
                    if (p < this_pointer || this_pointer - p >= 4*4)
                    {
                        continue;
                    }

                    //テーブルを発見.どこまで伸びるか調査する.
                    p = U.toOffset(p);
                    uint start = p;
                    for (p = p + 4; p < end; p += 4)
                    {
                        uint pp = Program.ROM.u32(p);
                        if (!U.isSafetyPointer(pp))
                        {
                            break;
                        }
                    }
                    uint count = (p - start) / 4;
                    if (count < 5)
                    {
                        continue;
                    }


                    //もう一度ループを回して今度は記録する.
                    //                    FEBuilderGBA.Address.AddAddress(list,addr, 4, U.NOT_FOUND, "switch pointer"));
                    uint endp = p;
                    for (p = start; p < endp; p += 4)
                    {
                        list[p] = U.NOT_FOUND;
                    }
                    break;
                }

            }

        }
        //フリー領域と思われる部分を検出.
        public static void MakeFreeDataList(List<Address> list, uint addr, byte filldata, uint needSize)
        {
            byte[] data = Program.ROM.Data;
            uint length = (uint)Program.ROM.Data.Length;

            MakeFreeDataList(list, addr, filldata, needSize, data, length);
        }

        //フリー領域と思われる部分を検出.
        public static void MakeFreeDataList(List<Address> list, uint addr, byte filldata, uint needSize,byte[] data, uint length)
        {
            string name = "FREEAREA:" + filldata.ToString("X02");

            addr = U.Padding4(addr);
            for (; addr < length; addr += 4)
            {
                if (data[addr] == filldata)
                {
                    uint start = addr;
                    addr++;
                    for (; ; addr++)
                    {
                        if (addr >= length)
                        {
                            uint matchsize = addr - start;
                            if (matchsize >= needSize)
                            {
                                if (InputFormRef.DoEvents(null, "MakeFreeDataList " + U.ToHexString(addr))) return;
                                FEBuilderGBA.Address.AddAddress(list
                                    , start
                                    , matchsize
                                    , U.NOT_FOUND
                                    , name
                                    , Address.DataTypeEnum.FFor00);
                            }
                            break;
                        }
                        if (data[addr] != filldata)
                        {
                            uint matchsize = addr - start;
                            if (matchsize >= needSize)
                            {
                                if (InputFormRef.DoEvents(null, "MakeFreeDataList " + U.ToHexString(addr))) return;
                                FEBuilderGBA.Address.AddAddress(list
                                    , start
                                    , matchsize
                                    , U.NOT_FOUND
                                    , name
                                    , Address.DataTypeEnum.FFor00);
                            }
                            break;
                        }
                    }

                    addr = U.Padding4(addr);
                }
            }
        }

        //近いデータの検索
        List<uint> NearSearchSortedList = new List<uint>();
        public void MakeNearSearchSortedList()
        {
            this.NearSearchSortedList.Clear();
            foreach (var pair in AsmMap)
            {
                this.NearSearchSortedList.Add(pair.Key);
            }
            this.NearSearchSortedList.Sort();
        }
        public uint SearchNear(uint pointer)
        {
            int length = this.NearSearchSortedList.Count;
            int i;
            for ( i = 0; i < length; i++)
            {
                uint p = this.NearSearchSortedList[i];
                if (pointer < p)
                {
                    break;
                }
                p = p + this.AsmMap[p].Length;
                if (pointer < p)
                {
                    i++;
                    break;
                }
            }

            if (i == 0)
            {
                return U.NOT_FOUND;
            }
            return this.NearSearchSortedList[i - 1];
        }

        //すべてのテキストの参照ID
        List<TextID> TextIDArray = null;
        public List<TextID> GetTextIDArray()
        {
            return this.TextIDArray;
        }
        public void MakeTextIDArray()
        {
            this.TextIDArray = U.MakeTextIDArray();
        }
    }
}