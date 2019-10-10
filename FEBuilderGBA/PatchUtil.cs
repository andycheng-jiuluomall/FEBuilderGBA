﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Reflection;
using System.IO;
using System.Collections.Concurrent;

namespace FEBuilderGBA
{
    public static class PatchUtil
    {
        static public void ClearCache()
        {
            g_Cache_portrait_extends = portrait_extends.NoCache;
            g_Cache_skill_system_enum = skill_system_enum.NoCache;
            g_Cache_draw_font_enum = draw_font_enum.NoCache;
            g_Cache_class_type_enum = class_type_enum.NoCache;
            g_Cache_itemicon_extends = itemicon_extends.NoCache;
            g_Cache_shinan_table = NO_CACHE;
            g_Cache_SkipWorldMap_enum = mnc2_fix_enum.NoCache;
            g_Cache_ImprovedSoundMixer = ImprovedSoundMixer.NoCache;
            g_LevelMaxCaps = NO_CACHE;
            g_Cache_AutoNewLine_enum = AutoNewLine_enum.NoCache;
            g_Cache_Escape_enum = Escape_enum.NoCache;
        }

        public const uint NO_CACHE = 0xff;
        public enum SpecialHack_enum
        {
            No,
            MoDUPS,  //FE6でマップデータの形式が拡張されているパッチ
            NoCache = (int)NO_CACHE
        }
        static SpecialHack_enum g_SpecialHack = SpecialHack_enum.NoCache;
        public static SpecialHack_enum SearchSpecialHack()
        {
            if (g_SpecialHack == SpecialHack_enum.NoCache)
            {
                g_SpecialHack = SearchSpecialHackLow();
            }
            return g_SpecialHack;
        }
        static SpecialHack_enum SearchSpecialHackLow()
        {
            if (Program.ROM.RomInfo.version() == 6)
            {
                if (Program.ROM.u16(0x2BB12) == 0x2048)
                {
                    return SpecialHack_enum.MoDUPS;
                }
            }
            return SpecialHack_enum.No;
        }

        //最大レベルの検索
        static uint g_LevelMaxCaps = NO_CACHE;
        public static uint GetLevelMaxCaps()
        {
            if (g_LevelMaxCaps == NO_CACHE)
            {
                if (PatchUtil.SearchSkillSystem() == PatchUtil.skill_system_enum.SkillSystem)
                {//不明なので31とする
                    g_LevelMaxCaps = 31;
                }
                else
                {
                    g_LevelMaxCaps = Program.ROM.u8(Program.ROM.RomInfo.max_level_address());
                }
            }
            return g_LevelMaxCaps;
        }

        //スキルシステムの判別. ちょっとだけコストがかかる.
        public enum skill_system_enum
        {
            NO,             //なし
            FE8N,           //for FE8J
            FE8N_ver2,      //for FE8J   FE8Nの2018/01 に新しく追加されたもの
            yugudora,       //for FE8J   FE8Nのカスタマイズ
            midori,         //for FE8J   初期から独自スキルを実装していた拡張
            SkillSystem,    //for FE8U
            NoCache = (int)NO_CACHE
        };
        static skill_system_enum g_Cache_skill_system_enum = skill_system_enum.NoCache;
        public static skill_system_enum SearchSkillSystem()
        {
            if (g_Cache_skill_system_enum == skill_system_enum.NoCache)
            {
                g_Cache_skill_system_enum = SearchSkillSystemLow();
            }
            return g_Cache_skill_system_enum;
        }
        static skill_system_enum SearchSkillSystemLow()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="yugudora",	ver = "FE8J", addr = 0xEE594,data = new byte[]{0x4B ,0xFA ,0x2F ,0x59}},
                new PatchTableSt{ name="FE8N",	ver = "FE8J", addr = 0x89268,data = new byte[]{0x00 ,0x4B ,0x9F ,0x46}},
                new PatchTableSt{ name="midori",	ver = "FE8J", addr = 0xFE58E0,data = new byte[]{0x05 ,0x1C ,0x00 ,0xF0 ,0x25 ,0xF8 ,0x01 ,0x29 ,0x04 ,0xD0 ,0x28 ,0x1C ,0x00 ,0xF0 ,0x28 ,0xF8}},
                new PatchTableSt{ name="SkillSystem",	ver = "FE8U", addr = 0x2ACF8,data = new byte[]{0x70 ,0x47}},
            };

            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                byte[] data = Program.ROM.getBinaryData(t.addr, t.data.Length);
                if (U.memcmp(t.data, data) != 0)
                {
                    continue;
                }
                if (t.name == "FE8N")
                {
                    if (SkillConfigFE8NVer2SkillForm.IsFE8NVer2())
                    {
                        return skill_system_enum.FE8N_ver2;
                    }
                    return skill_system_enum.FE8N;
                }
                if (t.name == "yugudora")
                {
                    return skill_system_enum.yugudora;
                }
                if (t.name == "midori")
                {
                    return skill_system_enum.midori;
                }
                if (t.name == "SkillSystem")
                {
                    return skill_system_enum.SkillSystem;
                }
            }
            return skill_system_enum.NO;
        }

        //スキルシステムの判別. ちょっとだけコストがかかる.
        public enum class_type_enum
        {
            NO,             //なし
            SkillSystems_Rework,          //for FE8U
            NoCache = (int)NO_CACHE
        };
        static class_type_enum g_Cache_class_type_enum = class_type_enum.NoCache;
        public static class_type_enum SearchClassType()
        {
            if (g_Cache_class_type_enum == class_type_enum.NoCache)
            {
                g_Cache_class_type_enum = SearchSkillSystemsEffectivenesReworkLow();
            }
            return g_Cache_class_type_enum;
        }
        static class_type_enum SearchSkillSystemsEffectivenesReworkLow()
        {
            if (Program.ROM.RomInfo.version() == 8 && Program.ROM.RomInfo.is_multibyte() == false)
            {
                bool r = Program.ROM.CompareByte(0x2AAEC
                    , new byte[] { 0x00, 0x25, 0x00, 0x28, 0x00, 0xD0, 0x05, 0x1C });
                if (r)
                {
                    return class_type_enum.SkillSystems_Rework;
                }
            }
            return class_type_enum.NO;
        }

        //un-Huffmanの判別.
        public static bool SearchAntiHuffmanPatch()
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_anti_Huffman(out check_value);
            if (address == 0)
            {
                return false;
            }
            uint a = Program.ROM.u32(address);
            return (a == check_value);
        }
        //フォントの描画ルーチン
        public enum draw_font_enum
        {
            NO,             //なし
            DrawMultiByte,  //FE7U/FE8Uに日本語を描画するパッチ
            DrawSingleByte, //FE7J/FE8Uに英語を描画するパッチ
            DrawUTF8,       //FE8UにUTF-8を描画するパッチ
            NoCache = (int)NO_CACHE
        };
        //DrawFontPatch(DrawMultiByte/DrawSingleByte)の判別.
        static draw_font_enum g_Cache_draw_font_enum = draw_font_enum.NoCache;
        public static draw_font_enum SearchDrawFontPatch()
        {
            if (g_Cache_draw_font_enum == draw_font_enum.NoCache)
            {
                g_Cache_draw_font_enum = SearchDrawFontPatch(Program.ROM);
            }
            return g_Cache_draw_font_enum;
        }
        public struct PatchTableSt
        {
            public string name;
            public string ver;
            public uint addr;
            public byte[] data;
        };
        public static draw_font_enum SearchDrawFontPatch(ROM rom)
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="DrawSingle",	ver = "FE7J", addr = 0x56e2,data = new byte[]{0x00, 0x00, 0x00, 0x49, 0x8F, 0x46}},
                new PatchTableSt{ name="DrawSingle",	ver = "FE8J", addr = 0x40c2,data = new byte[]{0x00 ,0x00 ,0x00 ,0x49 ,0x8F ,0x46}},
                new PatchTableSt{ name="DrawMulti",	ver = "FE7U", addr = 0x5BD6,data = new byte[]{0x00 ,0x00 ,0x00 ,0x4B ,0x9F ,0x46}},
                new PatchTableSt{ name="DrawMulti",	ver = "FE8U", addr = 0x44D2,data = new byte[]{0x00 ,0x00 ,0x00 ,0x49 ,0x8F ,0x46}},
                new PatchTableSt{ name="DrawUTF8",	ver = "FE7U", addr = 0x5B6A,data = new byte[]{0x00 ,0x00 ,0x00 ,0x4B ,0x18 ,0x47}},
                new PatchTableSt{ name="DrawUTF8",	ver = "FE8U", addr = 0x44D2,data = new byte[]{0x00 ,0x00 ,0x00 ,0x4B ,0x18 ,0x47}},
            };

            string version = rom.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                //チェック開始アドレス
                byte[] data = rom.getBinaryData(t.addr, t.data.Length);
                if (U.memcmp(t.data, data) != 0)
                {
                    continue;
                }
                if (t.name == "DrawSingle")
                {
                    return draw_font_enum.DrawSingleByte;
                }
                if (t.name == "DrawMulti")
                {
                    return draw_font_enum.DrawMultiByte;
                }
                if (t.name == "DrawUTF8")
                {
                    return draw_font_enum.DrawUTF8;
                }
            }
            return draw_font_enum.NO;
        }

        public enum PRIORITY_CODE
        {
            LAT1,
            SJIS,
            UTF8
        };
        //文字コードエンコードがパディングしてしまったときの優先変換方法.
        public static PRIORITY_CODE SearchPriorityCode()
        {
            if (Program.ROM.RomInfo.is_multibyte())
            {
                return PRIORITY_CODE.SJIS;
            }
            else
            {
                draw_font_enum dfe = SearchDrawFontPatch();
                if (dfe == draw_font_enum.DrawMultiByte)
                {
                    return PRIORITY_CODE.SJIS;
                }
                if (dfe == draw_font_enum.DrawUTF8)
                {
                    return PRIORITY_CODE.UTF8;
                }
                return PRIORITY_CODE.LAT1;
            }
        }

        public static PRIORITY_CODE SearchPriorityCode(ROM rom)
        {
            if (rom == null)
            {
                return PRIORITY_CODE.SJIS;
            }

            if (rom.RomInfo.is_multibyte())
            {
                return PRIORITY_CODE.SJIS;
            }
            else
            {
                draw_font_enum dfe = SearchDrawFontPatch(rom);
                if (dfe == draw_font_enum.DrawMultiByte)
                {
                    return PRIORITY_CODE.SJIS;
                }
                if (dfe == draw_font_enum.DrawUTF8)
                {
                    return PRIORITY_CODE.UTF8;
                }
                return PRIORITY_CODE.LAT1;
            }
        }

        //C01Hack(マント)
        public static bool SearchC01HackPatch()
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_C01_hack(out check_value);
            if (address == 0)
            {
                return false;
            }
            uint a = Program.ROM.u32(address);
            return (a == check_value);
        }
        //C48Hack
        public static bool SearchC48HackPatch()
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_C48_hack(out check_value);
            if (address == 0)
            {
                return false;
            }
            //C48だけはNOT条件です
            uint a = Program.ROM.u32(address);
            return (a != check_value);
        }
        //sound16trackパッチの判別.
        public static bool Search16tracks12soundsPatch()
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_16_tracks_12_sounds(out check_value);
            if (address == 0)
            {
                return false;
            }
            uint a = Program.ROM.u32(address);
            return (a == check_value);
        }

        //StairsHack
        public static bool SearchStairsHackPatch()
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_stairs_hack(out check_value);
            if (address == 0)
            {
                return false;
            }
            uint a = Program.ROM.u32(address);
            return (a == check_value);
        }


        //UnitActionRework
        public static bool SearchUnitActionReworkPatch()
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_unitaction_rework_hack(out check_value);
            if (address == 0)
            {
                return false;
            }
            uint a = Program.ROM.u32(address);
            return (a == check_value);
        }

        //ワールドマップスキップパッチが適応されているかどうか判定する
        public enum mnc2_fix_enum
        {
            NO,             //なし
            Aera_Version,   //aeraさんの作ったバージョン
            OldFix,         //古いルーチン
            Stan_20190505,  //Stanが2019/5/5 に提案した方式
            NoCache = (int)NO_CACHE
        };
        static mnc2_fix_enum g_Cache_SkipWorldMap_enum = mnc2_fix_enum.NoCache;
        public static mnc2_fix_enum SearchSkipWorldMapPatch()
        {
            if (g_Cache_SkipWorldMap_enum == mnc2_fix_enum.NoCache)
            {
                g_Cache_SkipWorldMap_enum = SearchSkipWorldMapPatchLow();
            }
            return g_Cache_SkipWorldMap_enum;
        }
        static mnc2_fix_enum SearchSkipWorldMapPatchLow()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="OldFix",	ver = "FE8J", addr = 0xc1e7c,data = new byte[]{0xB8, 0xE0}},
                new PatchTableSt{ name="Stan_20190505",	ver = "FE8J", addr = 0x0F664,data = new byte[]{0x94, 0xF6, 0x00, 0x08}}, //NOT条件
                new PatchTableSt{ name="Aera_Version" ,	ver = "FE8J", addr = 0xc03e0,data = new byte[]{0x01, 0x48, 0x80, 0x7B, 0x70, 0x47, 0x00, 0x00, 0xEC, 0xBC, 0x02, 0x02}},
                new PatchTableSt{ name="OldFix",	ver = "FE8U", addr = 0xBD070,data = new byte[]{0xB8, 0xE0}},
                new PatchTableSt{ name="Stan_20190505",	ver = "FE8U", addr = 0x0F464,data = new byte[]{0x98, 0xF4, 0x00, 0x08}}, //NOT条件
            };

            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                //チェック開始アドレス
                byte[] data = Program.ROM.getBinaryData(t.addr, t.data.Length);
                if (U.memcmp(t.data, data) != 0)
                {
                    if (t.name == "Stan_20190505")
                    {
                        return mnc2_fix_enum.Stan_20190505;
                    }
                    continue;
                }
                if (t.name == "OldFix")
                {
                    return mnc2_fix_enum.OldFix;
                }
                if (t.name == "Aera_Version")
                {
                    return mnc2_fix_enum.Aera_Version;
                }
            }
            return mnc2_fix_enum.NO;
        }

        public static bool SearchGenericEnemyPortraitExtendsPatch(out uint out_pointer)
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_generic_enemy_portrait_extends(out check_value);
            if (address == 0)
            {
                out_pointer = U.NOT_FOUND;
                return false;
            }
            uint a = Program.ROM.u32(address);
            if (a != check_value)
            {
                out_pointer = U.NOT_FOUND;
                return false;
            }
            out_pointer = address + 20;
            if (!U.isSafetyPointer(Program.ROM.u32(out_pointer)))
            {
                return false;
            }
            return true;
        }

        public static bool SearchNIMAP()
        {
            List<U.AddrResult> iset = SongUtil.SearchInstrumentSet(U.ConfigDataFilename("song_instrumentset_"), 100);
            for (int i = 0; i < iset.Count; i++)
            {
                string name = iset[i].name;
                if (name.IndexOf("NIMAP") >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        //武器魔法を同時に利用できるパッチの判別.
        public static bool SearchMeleeAndMagicFixPatch()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="GIRLS",	ver = "FE8J", addr = 0x18752,data = new byte[]{0x18}},
                new PatchTableSt{ name="FE8NMAGIC",	ver = "FE8J", addr = 0x2a542,data = new byte[]{0x30 ,0x1C}},
                new PatchTableSt{ name="MeleeAndMagicFix",	ver = "FE8J", addr = 0x1876C,data = new byte[]{0x00 ,0xB5 ,0xFE ,0xF7}},
                new PatchTableSt{ name="MeleeAndMagicFix",	ver = "FE7J", addr = 0x188CC,data = new byte[]{0x00 ,0xB5 ,0xFE ,0xF7}},
                new PatchTableSt{ name="MeleeAndMagicFix",	ver = "FE8U", addr = 0x18A58,data = new byte[]{0x00 ,0xB5 ,0xFE ,0xF7}},
                //new PatchTableSt{ name="UnkMeleeAndMagicFix",	ver = "FE8U", addr = 0x87852,data = new byte[]{0x60 ,0xB4 ,0x00 ,0x26}},
                new PatchTableSt{ name="MeleeAndMagicFix",	ver = "FE7U", addr = 0x184DC,data = new byte[]{0x00 ,0xB5 ,0xFE ,0xF7}},
                new PatchTableSt{ name="MeleeAndMagicFix",	ver = "FE6", addr = 0x18188,data = new byte[]{0x00 ,0xB5 ,0xFE ,0xF7}},
            };
            return SearchPatchBool(table);
        }

        static bool SearchPatchBool(PatchTableSt[] table)
        {
            PatchTableSt p = SearchPatch(table);
            return p.addr != 0;
        }

        static PatchTableSt SearchPatch(PatchTableSt[] table)
        {
            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                byte[] data = Program.ROM.getBinaryData(t.addr, t.data.Length);
                if (U.memcmp(t.data, data) != 0)
                {
                    continue;
                }
                return t;
            }
            return new PatchTableSt();
        }

        public struct GrepPatchTableSt
        {
            public string name;
            public string patch_dmp;
        };
        static GrepPatchTableSt GrepPatch(GrepPatchTableSt[] table)
        {
            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (GrepPatchTableSt t in table)
            {
                string fullfilename = Path.Combine(Program.BaseDirectory, "config", "patch2", version, t.patch_dmp);
                if (! File.Exists(fullfilename))
                {
                    continue;
                }
                byte[] data = File.ReadAllBytes(fullfilename);
                uint addr = U.Grep(Program.ROM.Data, data, Program.ROM.RomInfo.compress_image_borderline_address(), 0, 4);
                if (addr == U.NOT_FOUND)
                {
                    continue;
                }
                if (!U.isSafetyOffset(addr))
                {
                    continue;
                }

                return t;
            }
            return new GrepPatchTableSt();
        }

        //カメラを移動する命令で、画面外に飛び出してしまうバグを修正するパッチの検出
        public static bool SearchCAMERA_Event_OutOfBand_FixPatch()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="Fix CAM1/CAMERA2 going out of bounds",	ver = "FE8J", addr = 0x15D5E,data = new byte[]{0x14}},
                new PatchTableSt{ name="Fix CAM1/CAMERA2 going out of bounds",	ver = "FE8U", addr = 0x15D52,data = new byte[]{0x14}},
            };
            return SearchPatchBool(table);
        }

        //存在ユニットを選択したときフリーズしないように
        public static bool SearchCAMERA_Event_NotExistsUnit_FixPatch()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="Prevent Freeze For Camera Event 0x26",	ver = "FE8J", addr = 0xF468,data = new byte[]{0x00, 0x20}},
                new PatchTableSt{ name="Prevent Freeze For Camera Event 0x26",	ver = "FE8U", addr = 0xF25C,data = new byte[]{0x00, 0x20}},
            };
            return SearchPatchBool(table);
        }
        //存在ユニットを選択したときフリーズしないように
        public static bool SearchUnitStateEvent_0x34_FixPatch()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="Prevent Freeze For Unit State Event 0x34",	ver = "FE8J", addr = 0x10430,data = new byte[]{0x00, 0x20}},
                new PatchTableSt{ name="Prevent Freeze For Unit State Event 0x34",	ver = "FE8U", addr = 0x102D4,data = new byte[]{0x00, 0x20}},
            };
            return SearchPatchBool(table);
        }
        //存在ユニットを選択したときフリーズしないように
        public static bool SearchWakuEvent_0x3B_FixPatch()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="Prevent Freeze For Event 0x3B",	ver = "FE8J", addr = 0x10950,data = new byte[]{0x00, 0x20}},
                new PatchTableSt{ name="Prevent Freeze For Event 0x3B",	ver = "FE8U", addr = 0x10804,data = new byte[]{0x00, 0x20}},
            };
            return SearchPatchBool(table);
        }


        public enum Escape_enum
        {
            NO,             //なし
            EscapeArrivePath,
            EscapeMenuPath,
            NoCache = (int)NO_CACHE
        };
        static Escape_enum g_Cache_Escape_enum = Escape_enum.NoCache;
        public static Escape_enum SearchEscapePatch()
        {
            if (g_Cache_Escape_enum == Escape_enum.NoCache)
            {
                g_Cache_Escape_enum = SearchEscapePatchLow();
            }
            return g_Cache_Escape_enum;
        }
        static Escape_enum SearchEscapePatchLow()
        {
            {
                PatchTableSt[] table = new PatchTableSt[] { 
                    new PatchTableSt{ name="escape_arrive",	ver = "FE8U", addr = 0x187A8,data = new byte[]{0x00, 0x4b, 0x18, 0x47 }},
                };
                PatchTableSt p = SearchPatch(table);
                if (p.name == "escape_arrive")
                {
                    return Escape_enum.EscapeArrivePath;
                }
            }
            {
                GrepPatchTableSt[] table = new GrepPatchTableSt[] { 
                    new GrepPatchTableSt{ name="escape_menu",patch_dmp="EscapeMenu/IsLoca0x13.dmp"},
                };
                GrepPatchTableSt p = GrepPatch(table);
                if (p.name == "escape_menu")
                {
                    return Escape_enum.EscapeMenuPath;
                }
            }
            return Escape_enum.NO;
        }

        //SearchAutoNewLine
        public enum AutoNewLine_enum
        {
            NO,             //なし
            AutoNewLine,
            NoCache = (int)NO_CACHE
        };
        static AutoNewLine_enum g_Cache_AutoNewLine_enum = AutoNewLine_enum.NoCache;
        public static AutoNewLine_enum SearchAutoNewLinePatch()
        {
            if (g_Cache_AutoNewLine_enum == AutoNewLine_enum.NoCache)
            {
                g_Cache_SkipWorldMap_enum = SearchSkipWorldMapPatchLow();
            }
            return g_Cache_AutoNewLine_enum;
        }
        static AutoNewLine_enum SearchAutoNewLinePatchLow()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="AutoNewLine",	ver = "FE8U", addr = 0x464476,data = new byte[]{0x00, 0x00, 0x10, 0xB4, 0x00, 0xB5, 0x03, 0x4C, 0x00, 0xF0, 0x04, 0xF8, 0x10, 0xBC, 0xA6, 0x46, 0x10, 0xBC, 0x70, 0x47, 0x20, 0x47 }},
            };
            if (SearchPatchBool(table))
            {
                return AutoNewLine_enum.AutoNewLine;
            }
            return AutoNewLine_enum.NO;
        }

        //指南パッチの設定アドレスの場所
        static uint g_Cache_shinan_table = NO_CACHE;
        public static uint SearchShinanTablePatch()
        {
            if (g_Cache_shinan_table == NO_CACHE)
            {
                g_Cache_shinan_table = SearchShinanTablePatchLow();
            }
            return g_Cache_shinan_table;
        }
        static uint SearchShinanTablePatchLow()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="Shinan",	ver = "FE8J", addr = 0xDB000,data = new byte[]{0x00,0xB5,0xC0,0x46,0x06,0x48,0xC0,0x46,0x06,0x49,0x89,0x7B,0x89,0x00,0x40,0x58,0x01,0x21,0x00,0xF0,0x02,0xF8,0x17,0x20,0x00,0xBD,0xC0,0x46,0x02,0x4B,0x9F,0x46}},
                new PatchTableSt{ name="ShinanEA",	ver = "FE8J", addr = 0xDB000,data = new byte[]{0x00,0xB5,0x26,0x20,0x07,0x4B,0x9E,0x46,0x00,0xF8,0x09,0x48,0x06,0x49,0x89,0x7B,0x89,0x00,0x40,0x58,0x01,0x21,0x05,0x4B,0x9E,0x46,0x00,0xF8,0x17,0x20,0x02,0xBC,0x08,0x47,0x00,0x00,0xA8,0x60,0x08,0x08,0xEC,0xBC,0x02,0x02,0x40,0xD3,0x00,0x08}},
                new PatchTableSt{ name="Shinan",	ver = "FE8U", addr = 0xDB000,data = new byte[]{0x00,0xB5,0xC0,0x46,0x06,0x48,0xC0,0x46,0x06,0x49,0x89,0x7B,0x89,0x00,0x40,0x58,0x01,0x21,0x00,0xF0,0x02,0xF8,0x17,0x20,0x00,0xBD,0xC0,0x46,0x02,0x4B,0x9F,0x46}},
                new PatchTableSt{ name="ShinanEA",	ver = "FE8U", addr = 0xDB000,data = new byte[]{0x00,0xB5,0x26,0x20,0x07,0x4B,0x9E,0x46,0x00,0xF8,0x09,0x48,0x06,0x49,0x89,0x7B,0x89,0x00,0x40,0x58,0x01,0x21,0x05,0x4B,0x9E,0x46,0x00,0xF8,0x17,0x20,0x02,0xBC,0x08,0x47,0x00,0x00,0x80,0x3D,0x08,0x08,0xF0,0xBC,0x02,0x02,0x7C,0xD0,0x00,0x08}},
            };

            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                uint addr = U.GrepEnd(Program.ROM.Data, t.data, t.addr, 0, 4, 0, true);
                if (addr == U.NOT_FOUND)
                {
                    continue;
                }
                if (!U.isSafetyOffset(addr))
                {
                    continue;
                }
                addr = Program.ROM.p32(addr);
                if (!U.isSafetyOffset(addr))
                {
                    continue;
                }
                return U.toOffset(addr);
            }
            return U.NOT_FOUND;
        }

        //タイトルをテキストから自動生成するパッチがあるか判別.
        public static bool SearchChaptorNamesAsTextFixPatch()
        {
            uint check_value;
            uint address = Program.ROM.RomInfo.patch_chaptor_names_text_fix(out check_value);
            if (address == 0)
            {
                return false;
            }
            uint a = Program.ROM.u32(address);
            return (a == check_value);
        }
        //顔画像拡張システム.
        public enum portrait_extends
        {
            NO,             //なし
            MUG_EXCEED,     //tikiの顔画像拡張
            HALFBODY,       //上半身表示拡張
            NoCache = (int)NO_CACHE
        };
        static portrait_extends g_Cache_portrait_extends = portrait_extends.NoCache;
        public static portrait_extends SearchPortraitExtends()
        {
            if (g_Cache_portrait_extends == portrait_extends.NoCache)
            {
                g_Cache_portrait_extends = SearchPortraitExtendsLow();
            }
            return g_Cache_portrait_extends;
        }
        static portrait_extends SearchPortraitExtendsLow()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="MUG_EXCEED",	ver = "FE8J", addr = 0x54da,data = new byte[]{0xC0 ,0x46 ,0x01 ,0xB0 ,0x03 ,0x4B}},
                new PatchTableSt{ name="MUG_EXCEED",	ver = "FE8U", addr = 0x55D2,data = new byte[]{0xC0 ,0x46 ,0x01 ,0xB0 ,0x03 ,0x4B}},
                new PatchTableSt{ name="MUG_EXCEED",	ver = "FE7U", addr = 0x6BCA,data = new byte[]{0xC0 ,0x46 ,0x01 ,0xB0 ,0x03 ,0x4B}},
                new PatchTableSt{ name="MUG_EXCEED",	ver = "FE7J", addr = 0x6A5A,data = new byte[]{0xC0 ,0x46 ,0x01 ,0xB0 ,0x03 ,0x4B}},
                new PatchTableSt{ name="HALFBODY",	ver = "FE8U", addr = 0x8540,data = new byte[]{0x0A ,0x1C}},
                new PatchTableSt{ name="HALFBODY",	ver = "FE8J", addr = 0x843C,data = new byte[]{0x0A ,0x1C}},
                new PatchTableSt{ name="HALFBODY",	ver = "FE8U", addr = 0x8540,data = new byte[]{0x01 ,0x3A}},
                new PatchTableSt{ name="HALFBODY",	ver = "FE8J", addr = 0x843C,data = new byte[]{0x01 ,0x3A}},
            };

            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                byte[] data = Program.ROM.getBinaryData(t.addr, t.data.Length);
                if (U.memcmp(t.data, data) != 0)
                {
                    continue;
                }

                if (t.name == "MUG_EXCEED")
                {
                    return portrait_extends.MUG_EXCEED;
                }
                if (t.name == "HALFBODY")
                {
                    return portrait_extends.HALFBODY;
                }
            }
            return portrait_extends.NO;
        }


        //音楽ルーチンの改良パッチの有無.
        public enum ImprovedSoundMixer
        {
            NO,             //なし
            ImprovedSoundMixer,
            NoCache = (int)NO_CACHE
        };
        static ImprovedSoundMixer g_Cache_ImprovedSoundMixer = ImprovedSoundMixer.NoCache;
        public static ImprovedSoundMixer SearchImprovedSoundMixer()
        {
            if (g_Cache_ImprovedSoundMixer == ImprovedSoundMixer.NoCache)
            {
                g_Cache_ImprovedSoundMixer = SearchImprovedSoundMixerLow();
            }
            return g_Cache_ImprovedSoundMixer;
        }
        public static ImprovedSoundMixer SearchImprovedSoundMixerLow()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="ImprovedSoundMixer",	ver = "FE8J", addr = 0xD4234,data = new byte[]{0xb1, 0x6c, 0x00, 0x03, 0x06, 0x00}},
                new PatchTableSt{ name="ImprovedSoundMixer",	ver = "FE8U", addr = 0xd01d0,data = new byte[]{0xb0, 0x6c, 0x00, 0x03, 0x18, 0x02}},
            };

            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                //チェック開始アドレス
                byte[] data = Program.ROM.getBinaryData(t.addr, t.data.Length);
                if (U.memcmp(t.data, data) != 0)
                {
                    continue;
                }
                if (t.name == "ImprovedSoundMixer")
                {
                    return ImprovedSoundMixer.ImprovedSoundMixer;
                }
            }
            return ImprovedSoundMixer.NO;
        }


        //顔画像拡張システム.
        public enum itemicon_extends
        {
            NO,             //なし
            IconExpands,    //FEまで拡張
            SkillSystems,   //SkillSystems
            NoCache = (int)NO_CACHE
        };
        static itemicon_extends g_Cache_itemicon_extends = itemicon_extends.NoCache;
        public static itemicon_extends SearchItemIconExtends()
        {
            if (g_Cache_itemicon_extends == itemicon_extends.NoCache)
            {
                g_Cache_itemicon_extends = SearchItemIconExpandsPatchLow();
            }
            return g_Cache_itemicon_extends;
        }
        public static bool SearchIconExpandsPatch()
        {
            return SearchItemIconExtends() != itemicon_extends.NO;
        }
        public struct PatchItemIconExpandsSt
        {
            public string name;
            public string ver;
            public uint addr;
            public byte[] data;
        };
        public static itemicon_extends SearchItemIconExpandsPatchLow()
        {
            PatchTableSt[] table = new PatchTableSt[] { 
                new PatchTableSt{ name="IconExpands",	ver = "FE8J", addr = 0x34FC,data = new byte[]{0xFE, 0x01, 0x00, 0x01, 0x90, 0x6E, 0x02, 0x02}},
                new PatchTableSt{ name="IconExpands",	ver = "FE8U", addr = 0x35B0,data = new byte[]{0xFE, 0x01, 0x00, 0x01, 0x90, 0x6E, 0x02, 0x02}},
                new PatchTableSt{ name="SkillSystems",	ver = "FE8U", addr = 0x3586,data = new byte[]{0x03, 0x4C, 0x00, 0xF0, 0x03, 0xF8, 0x10, 0xBC, 0x02, 0xBC, 0x08, 0x47, 0x20, 0x47}},
            };

            string version = Program.ROM.RomInfo.VersionToFilename();
            foreach (PatchTableSt t in table)
            {
                if (t.ver != version)
                {
                    continue;
                }

                //チェック開始アドレス
                byte[] data = Program.ROM.getBinaryData(t.addr, t.data.Length);
                if (U.memcmp(t.data, data) != 0)
                {
                    continue;
                }
                if (t.name == "IconExpands")
                {
                    return itemicon_extends.IconExpands;
                }
                if (t.name == "SkillSystems")
                {
                    return itemicon_extends.SkillSystems;
                }
            }
            return itemicon_extends.NO;
        }


        public static MoveToUnuseSpace.ADDR_AND_LENGTH get_data_pos_callback(uint addr)
        {
            int length = 0;
            string str = Program.ROM.getString(addr, out length);

            MoveToUnuseSpace.ADDR_AND_LENGTH aal = new MoveToUnuseSpace.ADDR_AND_LENGTH();
            aal.addr = addr;
            aal.length = (uint)length + 1; //nullを入れる.
            return aal;
        }

        //共有しているデータの分割
        public static uint WriteIndependence(uint readAddr, uint blockSize, uint writeSettingAddr, string warnningName, Undo.UndoData undodata)
        {
            DialogResult dr = R.ShowYesNo("{0}のデータを、分離独立させますか？", warnningName);
            if (dr != DialogResult.Yes)
            {
                return U.NOT_FOUND;
            }

            byte[] d = Program.ROM.getBinaryData(readAddr, blockSize);
            uint addr = InputFormRef.AppendBinaryData(d, undodata);
            if (addr == U.NOT_FOUND)
            {
                return U.NOT_FOUND;
            }

            Program.ROM.write_p32(writeSettingAddr, addr, undodata);

            return addr;
        }

        public static bool IsSwitch2Enable(uint array_switch2_address)
        {
            //こういうのを探す
            //sub r0, #0x19
            //cmp r0, #0x37
            uint extraByte = 0;
            if (Program.ROM.u16(array_switch2_address + 2) == 0x9A00)
            {//古いコンパイラは、 9A00   ldr r2,[sp, #0x0] を挟むときがあるらしい.
                //sub r0, #0x19
                //ldr r2,[sp, #0x0]
                //cmp r0, #0x37
                extraByte = 2;
            }

            uint op = Program.ROM.u8(array_switch2_address + 1);
            if (op < 0x38 || op > 0x3D)
            {//SUB以外無視
                return false;
            }
            op = Program.ROM.u8(array_switch2_address + 3 + extraByte);
            if (op < 0x28 || op > 0x2d)
            {//CMP以外無視
                return false;
            }
            return true;
        }

        //switch文の拡張
        public static uint Switch2Expands(uint array_pointer
                        , uint array_switch2_address
                        , uint newCount
                        , uint defaultJumpAddr
                        , Undo.UndoData undodata)
        {
            uint pointeraddr = Program.ROM.p32(array_pointer);

            uint extraByte = 0;
            if (Program.ROM.u16(array_switch2_address + 2) == 0x9A00)
            {//古いコンパイラは、 9A00   ldr r2,[sp, #0x0] を挟むときがあるらしい.
                //sub r0, #0x19
                //ldr r2,[sp, #0x0]
                //cmp r0, #0x37
                extraByte = 2;
            }

            //384b     	sub	r0, #4b //ライブ 利用した時の効果
            //2876     	cmp	r0, #76
            uint start = Program.ROM.u8(array_switch2_address + 0);
            uint count = Program.ROM.u8(array_switch2_address + 2 + extraByte) + 1;
            if (newCount <= start + count)
            {
                R.ShowStopError("既に十分な数を確保しています。\r\nあなたの要求:{0} 既存サイズ:{1}+{2}={3}"
                    , U.To0xHexString(newCount), U.To0xHexString(start), U.To0xHexString(count), U.To0xHexString(start + count));
                return U.NOT_FOUND;
            }

            //オペコードの確認
            uint op = Program.ROM.u8(array_switch2_address + 1);
            if (op < 0x38 || op > 0x3D)
            {
                R.ShowStopError("別のパッチでオペコードを書き換えられているので、拡張できません\r\nアドレス:{0} オペコード:{1}"
                    , U.To0xHexString(array_switch2_address + 1), U.To0xHexString(op));
                return U.NOT_FOUND;
            }
            op = Program.ROM.u8(array_switch2_address + 3 + extraByte);
            if (op < 0x28 || op > 0x2d)
            {
                R.ShowStopError("別のパッチでオペコードを書き換えられているので、拡張できません\r\nアドレス:{0} オペコード:{1}"
                    , U.To0xHexString(array_switch2_address + 3), U.To0xHexString(op));
                return U.NOT_FOUND;
            }
            //ユーザに確認を求める.
            DialogResult dr = R.ShowYesNo("配列を {0}まで拡張してもよろしいですか？"
                , U.To0xHexString(newCount));
            if (dr != DialogResult.Yes)
            {
                return U.NOT_FOUND;
            }

            byte[] dd = Program.ROM.getBinaryData(pointeraddr, count * 4);
            byte[] d = new byte[(newCount + 1) * 4];
            for (uint i = 0; i < start; i++)
            {
                U.write_p32(d, i * 4, defaultJumpAddr);
            }
            Array.Copy(dd, 0, d, start * 4, count * 4);
            for (uint i = start + count; i < newCount; i++)
            {
                U.write_p32(d, i * 4, defaultJumpAddr);
            }

            uint newaddr = InputFormRef.AppendBinaryData(d, undodata);
            if (newaddr == U.NOT_FOUND)
            {
                return U.NOT_FOUND;
            }

            Program.ROM.write_p32(array_pointer, newaddr, undodata);
            Program.ROM.write_u8(array_switch2_address + 0, 0, undodata);
            Program.ROM.write_u8(array_switch2_address + 2 + extraByte, newCount - 1, undodata);

            return newaddr;
        }
        public static bool IsSwitch1Enable(uint array_switch1_address)
        {
            uint op = Program.ROM.u8(array_switch1_address + 1);
            if (op < 0x28 || op > 0x2d)
            {//CMP
                return false;
            }
            return true;
        }

        //switch文の拡張
        public static uint Switch1Expands(uint array_pointer
                        , uint array_switch1_address
                        , uint newCount
                        , uint defaultJumpAddr
                        , Undo.UndoData undodata)
        {
            uint pointeraddr = Program.ROM.p32(array_pointer);
            //0802fbc4 2876     	cmp	r0, #76
            uint start = 0;
            uint count = Program.ROM.u8(array_switch1_address + 0);
            if (newCount <= count)
            {
                R.ShowStopError("既に十分な数を確保しています。\r\nあなたの要求:{0} 既存サイズ:{1}+{2}={3}"
                    , U.To0xHexString(newCount), U.To0xHexString(start), U.To0xHexString(count), U.To0xHexString(start + count));
                return U.NOT_FOUND;
            }

            //オペコードの確認
            uint op = Program.ROM.u8(array_switch1_address + 1);
            if (op < 0x28 || op > 0x2d)
            {
                R.ShowStopError("別のパッチでオペコードを書き換えられているので、拡張できません\r\nアドレス:{0} オペコード:{1}"
                    , U.To0xHexString(array_switch1_address + 1), U.To0xHexString(op));
                return U.NOT_FOUND;
            }
            //ユーザに確認を求める.
            DialogResult dr = R.ShowYesNo("配列を {0}まで拡張してもよろしいですか？"
                , U.To0xHexString(newCount));
            if (dr != DialogResult.Yes)
            {
                return U.NOT_FOUND;
            }

            byte[] dd = Program.ROM.getBinaryData(pointeraddr, count * 4);
            byte[] d = new byte[(newCount + 1) * 4];
            for (uint i = 0; i < start; i++)
            {
                U.write_p32(d, i * 4, defaultJumpAddr);
            }
            Array.Copy(dd, 0, d, start * 4, count * 4);
            for (uint i = start + count; i < newCount; i++)
            {
                U.write_p32(d, i * 4, defaultJumpAddr);
            }

            uint newaddr = InputFormRef.AppendBinaryData(d, undodata);
            if (newaddr == U.NOT_FOUND)
            {
                return U.NOT_FOUND;
            }

            undodata.list.Add(new Undo.UndoPostion(array_pointer, 4));
            undodata.list.Add(new Undo.UndoPostion(array_switch1_address + 0, 1));
            undodata.list.Add(new Undo.UndoPostion(array_switch1_address + 2, 1));

            Program.ROM.write_p32(array_pointer, newaddr);
            Program.ROM.write_u8(array_switch1_address + 0, newCount - 1);

            return newaddr;
        }
    }
}
