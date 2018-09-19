﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace FEBuilderGBA
{
    public partial class EventAssemblerForm : Form
    {
        public EventAssemblerForm()
        {
            InitializeComponent();

            U.ForceUpdate(FREEAREA, InputFormRef.AllocBinaryData(1024 * 1024)); //とりあえず1MBの空きがあるところ.
            SRCFilename.AllowDropFilename();

            AllowDropFilename();
        }

        void AllowDropFilename()
        {
            U.AllowDropFilename(this
                , new string[] { ".EVENT", ".TXT" }
                , (string filename) =>
                {
                    SRCFilename.Text = filename;
                    this.WriteButton.PerformClick();
                });
        }

        private void SRCSelectButton_Click(object sender, EventArgs e)
        {
            string title = R._("インポートするeventファイルを選択してください");
            string filter = R._("event file|*.event;*.txt|event|*.event|text|*.txt|All files|*");

            OpenFileDialog open = new OpenFileDialog();
            open.Title = title;
            open.Filter = filter;
            Program.LastSelectedFilename.Load(this, "", open);
            DialogResult dr = open.ShowDialog();
            if (dr != DialogResult.OK)
            {
                return;
            }
            if (!U.CanReadFileRetry(open))
            {
                return;
            }
            Program.LastSelectedFilename.Save(this, "", open);
            string EAFilename = open.FileNames[0];

            SRCFilename.Text = EAFilename;
        }
        private void ImportButton_Click(object sender, EventArgs e)
        {
            if (!File.Exists(SRCFilename.Text))
            {
                SRCSelectButton_Click(sender,e);
            }
            string EAFilename = SRCFilename.Text;
            if (!File.Exists(SRCFilename.Text))
            {
                return;
            }

            Undo.UndoData undodata = Program.Undo.NewUndoData(this);

            try
            {
                uint freearea = (uint)FREEAREA.Value;
                if (!this.FREEAREA_DEF.Checked)
                {//フリーエリアを利用しない.
                    freearea = 0;
                }
                WriteEA(EAFilename,freearea, undodata);
            }
            catch (PatchForm.PatchException exception)
            {
                Program.Undo.Rollback(undodata);

                R.ShowStopError(exception.Message);
                return;
            }

            Program.Undo.Push(undodata);
            InputFormRef.ShowWriteNotifyAnimation(this, 0);

            UndoButton.Show();
            U.ForceUpdate(FREEAREA, InputFormRef.AllocBinaryData(1024 * 1024)); //とりあえず1MBの空きがあるところ.
        }

        private void UndoButton_Click(object sender, EventArgs e)
        {
            string text = R._("最後の動作を取り消してよろしいですか？");
            string title = R._("UNDO確認");

            DialogResult r =
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.YesNo
                    , MessageBoxIcon.Exclamation);
            if (r != System.Windows.Forms.DialogResult.Yes)
            {
                return;
            }

            Program.Undo.RunUndo();  //操作の取り消し
            UndoButton.Hide();

            InputFormRef.ShowWriteNotifyAnimation(this, 0);
            U.ForceUpdate(FREEAREA,InputFormRef.AllocBinaryData(1024 * 1024)); //とりあえず1MBの空きがあるところ.
        }

        private void EventAssemblerForm_Load(object sender, EventArgs e)
        {
            string event_assembler = Program.Config.at("event_assembler", "");
            if (event_assembler == "")
            {
                R.ShowStopError("{0}の設定がありません。 設定->オプションから、{0}を設定してください。", "event_assembler");
                this.Close();
            }
        }

        private void FREEAREA_DEF_CheckedChanged(object sender, EventArgs e)
        {
            if (this.FREEAREA_DEF.Checked)
            {
                FREEARE_PANEL.Hide();
            }
            else
            {
                FREEARE_PANEL.Show();
            }
        }

        public static void WriteEA(string EA, uint freearea, Undo.UndoData undodata)
        {
            string output;
            bool r;
            try
            {
                r = MainFormUtil.CompilerEventAssembler(EA, freearea, out output);
            }
            catch (Win32Exception e)
            {
                r = false;
                output = R._("プロセスを実行できません。\r\nfilename:{0}\r\n{1}", EA,  e.ToString());
            }
            if (!r)
            {
                throw new PatchForm.PatchException(output);
            }

            r = Program.ROM.SwapNewROMData( File.ReadAllBytes(output)
                , "event_assembler", undodata);
            if (!r)
            {
                throw new PatchForm.PatchException(R.Notify("変更をユーザーが取り消しました"));
            }

            //EAの結果作成したROMを消します. 残すとROMがPATCHディレクトリに残るのでいろいろよろしくない.
            File.Delete(output);
        }
        public static string BlockToEA(uint addr,byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();

            if (addr != U.NOT_FOUND)
            {
                sb.Append("ORG " + U.To0xHexString(addr) + "\r\n");
            }
            for (int k = 0; k < bytes.Length; )
            {
                //16バイトごとに書こうか.
                sb.Append("BYTE");
                for (; k < 16; k++)
                {
                    if (k >= bytes.Length)
                    {
                        break;
                    }
                    sb.Append(" ");
                    sb.Append(bytes[k]);
                }
                sb.Append("\r\n");
            }
            sb.Append("\r\n");
            return sb.ToString();
        }

        //EAのORGを探します.
        public static uint FindORG(string eafilename)
        {
            bool isBlockComment = false;

            string[] lines = File.ReadAllLines(eafilename);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (isBlockComment)
                {//ブロックコメント中?
                    int commentEnd = line.IndexOf("*/");
                    if (commentEnd < 0)
                    {
                        continue;
                    }
                    //ブロックコメントの終了.
                    isBlockComment = false;
                    line = line.Substring(commentEnd);
                }

                if (U.IsComment(line))
                {
                    continue;
                }
                line = U.ClipComment(line);
                line = line.Trim();
                line = line.ToLower();
                if (line.Length <= 0)
                {
                    continue;
                }

                string[] tokens = line.Split(new string[] { " ", "\t", "(", ")", "+", ">", "<", "=", "," }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length <= 0)
                {
                    continue;
                }
                string op = tokens[0];
                if (op == "org" && tokens.Length >= 2)
                {
                    return U.atoi0x(tokens[1]);
                }
                else if (op[0] == '#') 
                {//#include とか.
                    continue;
                }

                if (line.IndexOf("/*") >= 0)
                {
                    isBlockComment = true;
                    continue;
                }

                //orgがない?
                break;
            }
            //orgがない
            return 0;
        }

    }
}