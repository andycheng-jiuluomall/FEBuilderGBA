﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;

using System.Text;
using System.Windows.Forms;

namespace FEBuilderGBA
{
    public partial class ImageUnitWaitIconFrom : Form
    {
        public ImageUnitWaitIconFrom()
        {
            InitializeComponent();
            this.AddressList.OwnerDraw(ListBoxEx.DrawImageUnitWaitIconAndText, DrawMode.OwnerDrawFixed);

            this.X_PALETTE.SelectedIndex = 0;
            this.InputFormRef = Init(this);
            this.InputFormRef.AddressListExpandsEvent += AddressListExpandsUpdateBaristaAnimationAddress;
            this.InputFormRef.MakeGeneralAddressListContextMenu(true);

            U.SetIcon(ExportButton, U.GetShell32Icon(122));
            U.SetIcon(ImportButton, U.GetShell32Icon(45));
        }
        //リストが拡張されたとき
        void AddressListExpandsUpdateBaristaAnimationAddress(object sender, EventArgs arg)
        {
            InputFormRef.ExpandsEventArgs eearg = (InputFormRef.ExpandsEventArgs)arg;
            uint addr = eearg.NewBaseAddress;
            int count = (int)eearg.NewDataCount;

            Program.ROM.write_p32(
                Program.ROM.RomInfo.unit_wait_barista_anime_address()
                , addr 
                    + 0x2 + (0x8 * Program.ROM.RomInfo.unit_wait_barista_id())
            );
        }

        public InputFormRef InputFormRef;
        static InputFormRef Init(Form self)
        {
            return new InputFormRef(self
                , ""
                , Program.ROM.RomInfo.unit_wait_icon_pointer()
                , 8
                , (int i,uint addr) =>
                {//読込最大値検索
                    //4 がポインタであればデータがあると考える.
                    return U.isPointerOrNULL(Program.ROM.u32(addr + 4));
                }
                , (int i, uint addr) =>
                {
                    uint icon_id = (uint)(i);
                    String name = ClassForm.GetClassNameWhereWaitIconID(icon_id);

                    return U.ToHexString(icon_id) + U.SA(name) + InputFormRef.GetCommentSA(addr) ;
                }
                );
        }

        private void ImageUnitWaitIconFrom_Load(object sender, EventArgs e)
        {
        }

        private void AddressList_SelectedIndexChanged(object sender, EventArgs e)
        {
            byte b2 = (byte)B2.Value;
            uint pic_address = (uint)D4.Value;
            X_ONE_STEP.Value = 0;

            int palette_type = X_PALETTE.SelectedIndex;

            X_PIC.Image = LoadWaitUnitIcon(pic_address, palette_type, b2);
            X_ONE_PIC.Image = DrawWaitUnitIcon(pic_address,b2,(int)X_ONE_STEP.Value, palette_type,false);
        }
        private void X_ONE_STEP_ValueChanged(object sender, EventArgs e)
        {
            byte b2 = (byte)B2.Value;
            uint pic_address = (uint)D4.Value;
            int step = (int)X_ONE_STEP.Value;
            int palette_type = X_PALETTE.SelectedIndex;
            X_ONE_PIC.Image = DrawWaitUnitIcon(pic_address, b2, step, palette_type,false);
        }

        public static Bitmap DrawWaitUnitIcon(
            uint pic_address,  byte b2, int step ,int palette_type,bool height16_limit
            )
        {
            Bitmap bmp = LoadWaitUnitIcon(pic_address, palette_type,b2);
            Rectangle rect;

            if (b2 == 1)
            {//16xN
                int height = ((2 * 8) + 16) * step + 8;

                if (height16_limit)
                {
                    rect = new Rectangle(0, height + (8 / 2), 2 * 8, 16);
                }
                else
                {//フルサイズ
                    rect = new Rectangle(0, height, 2 * 8, (2 * 8) + 8);
                }
            }
            else if (b2 == 2)
            {//32x32
                int height = 32*step;

                if (height16_limit)
                {
                    rect = new Rectangle(8, height+8+4, 2 * 8, 2 * 8);
                }
                else
                {//フルサイズ
                    rect = new Rectangle(0, height, 32, 32);
                }
            }
            else
            {//b2 == 0
                int height = ((2 * 8)) * step;
                rect = new Rectangle(0, height, 2 * 8, (2 * 8) + (b2 * 8));
            }

            return ImageUtil.Copy(bmp, rect.X, rect.Y, rect.Width, rect.Height);
        }

        public static Bitmap LoadWaitUnitIcon(uint pic_address, int palette_type,byte b2)
        {
            uint palette;
            if( palette_type == 1)
            {//友軍
                palette = Program.ROM.RomInfo.unit_icon_npc_palette_address();
            }
            else if( palette_type == 2)
            {//敵軍
                palette = Program.ROM.RomInfo.unit_icon_enemey_palette_address();
            }
            else 
            {//自軍
               palette = Program.ROM.RomInfo.unit_icon_palette_address();
            }
            uint pic_address_offset = U.toOffset(pic_address);

            if (!U.isSafetyOffset(palette))
            {
                return ImageUtil.Blank(2*8,2*8);
            }
            if (!U.isSafetyOffset(pic_address_offset))
            {
                return ImageUtil.Blank(2 * 8, 2 * 8);
            }
            byte[] imageUZ = LZ77.decompress(Program.ROM.Data, pic_address_offset);
            if (imageUZ.Length <= 0)
            {
                return ImageUtil.Blank(2 * 8, 2 * 8);
            }

            if (b2 == 2)
            {//32xN
                int width = 2 * 2 * 8;
                int height = ImageUtil.CalcHeight(width, imageUZ.Length);

                return ImageUtil.ByteToImage16Tile(width, height
                    , imageUZ, 0
                    , Program.ROM.Data, (int)palette
                    , 0
                    );
            }
            else
            {//16xN
                int width = 2 * 8;
                int height = ImageUtil.CalcHeight(width, imageUZ.Length);

                return ImageUtil.ByteToImage16Tile(width, height
                    , imageUZ, 0
                    , Program.ROM.Data, (int)palette
                    , 0
                    );
            }
        }
        public static Bitmap DrawWaitUnitIconBitmap(uint icon_id, int palette_type, bool height16_limit
            )
        {
            InputFormRef InputFormRef = Init(null);
            uint addr = InputFormRef.IDToAddr(icon_id);
            if (!U.isSafetyOffset(addr))
            {
                return ImageUtil.Blank(2 * 8, 2 * 8);
            }
            byte b2 = (byte) Program.ROM.u8(addr + 2);
            uint pic_address = (uint)Program.ROM.u32(addr + 4);

            return DrawWaitUnitIcon(pic_address, b2, 0, palette_type, height16_limit);
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            byte b2 = (byte)B2.Value;
            uint pic_address = (uint)D4.Value;
            X_ONE_STEP.Value = 0;

            int palette_type = X_PALETTE.SelectedIndex;

            Bitmap bitmap = LoadWaitUnitIcon(pic_address, palette_type, b2);
            ImageFormRef.ExportImage(this,bitmap, InputFormRef.MakeSaveImageFilename());
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            Bitmap bitmap = ImageFormRef.ImportFilenameDialog(this);
            if (bitmap == null)
            {
                return;
            }

            uint b2 = U.NOT_FOUND;
            if (bitmap.Width == 16)
            {
                if (bitmap.Height == 48)
                {
                    b2 = 0;
                }
                else if (bitmap.Height == 96)
                {
                    b2 = 1;
                }
            }
            else if (bitmap.Width == 32)
            {
                if (bitmap.Height == 96)
                {
                    b2 = 2;
                }
            }
            if (b2 == U.NOT_FOUND)
            {
                R.ShowStopError("画像サイズが正しくありません。\r\n以下のどれかにする必要があります。\r\n16x48\r\n16x96\r\n32x96\r\n\r\n選択された画像のサイズ Width:{0} Height:{1}", bitmap.Width, bitmap.Height);
                return;
            }

            //check palette
            {
                string palette_error =
                    ImageUtil.CheckPalette(bitmap.Palette
                        , Program.ROM.Data
                        , (Program.ROM.RomInfo.unit_icon_palette_address())
                        , (Program.ROM.RomInfo.unit_icon_enemey_palette_address())
                        );
                if (palette_error != "")
                {
                    ErrorPaletteShowForm f = (ErrorPaletteShowForm)InputFormRef.JumpFormLow<ErrorPaletteShowForm>();
                    f.SetErrorMessage(palette_error);
                    f.SetOrignalImage(ImageUtil.OverraidePalette(bitmap, Program.ROM.Data, (Program.ROM.RomInfo.unit_icon_palette_address())));
                    f.SetReOrderImage1(ImageUtil.ReOrderPalette(bitmap, Program.ROM.Data, (Program.ROM.RomInfo.unit_icon_palette_address())));
                    f.SetReOrderImage2(ImageUtil.ReOrderPalette(bitmap, Program.ROM.Data, (Program.ROM.RomInfo.unit_icon_enemey_palette_address())));
                    f.ShowForceButton();
                    f.ShowDialog();

                    bitmap = f.GetResultBitmap();
                    if (bitmap == null)
                    {
                        return;
                    }
                }
            }

            byte[] image = ImageUtil.ImageToByte16Tile(bitmap, bitmap.Width, bitmap.Height);

            //画像等データの書き込み
            Undo.UndoData undodata = Program.Undo.NewUndoData(this);
            this.InputFormRef.WriteImageData(this.D4, image, true, undodata);
            Program.Undo.Push(undodata);

            this.B2.Value = b2;


            //ポインタの書き込み
            this.WriteButton.PerformClick();
        }

        private void X_JUMP_MOVEICON_Click(object sender, EventArgs e)
        {
            if (this.AddressList.SelectedIndex < 0)
            {
                return;
            }
            uint cid = ClassForm.GetClassIDWhereWaitIconID((uint)this.AddressList.SelectedIndex);
            uint icon = ClassForm.GetClassMoveIcon(cid);
            InputFormRef.JumpTo(null,icon, "MOVEICON", new string[] { });
        }

        //全データの取得
        public static void MakeAllDataLength(List<Address> list,bool isPointerOnly)
        {
            string name = "WaitUnitIcon";
            {
                InputFormRef InputFormRef = Init(null);
                FEBuilderGBA.Address.AddAddress(list, InputFormRef, name, new uint[] { 4 });

                uint addr = InputFormRef.BaseAddress;
                for (int i = 0; i < InputFormRef.DataCount; i++, addr += InputFormRef.BlockSize)
                {
                    name = "WaitUnitIcon " + U.To0xHexString(i);

                    FEBuilderGBA.Address.AddLZ77Pointer(list
                        , addr + 4
                        , name
                        , isPointerOnly
                        , FEBuilderGBA.Address.DataTypeEnum.LZ77IMG);
                }
            }
        }

    }
}