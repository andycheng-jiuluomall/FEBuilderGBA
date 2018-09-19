@Call 0x08020252 FE8U
@r0
@r1
@r2
@r3
@r4 nazo

.macro blh to, reg=r3
  ldr \reg, =\to
  mov lr, \reg
  .short 0xf800
.endm


.thumb
push {r4}

ldr  r4,Table
sub  r4,#0xC        @     ���[�v�������ʓ|�Ȃ̂ŁA�ŏ���0xC(12)�o�C�g���������܂�.

Loop:
add  r4,#0xC        @     ���̃f�[�^��
ldr  r0,[r4,#0x04]  @     P4:ZIMAGE=Image
cmp  r0,#0x00       @     �f�[�^�̃|�C���^���Ȃ��ꍇ�A�I�[�Ƃ݂Ȃ�.
beq  load_defualt_bg     @�f�[�^���Ȃ��̂ŁA�f�B�t�H���g�̔w�i�����[�h���ďI��!

CheckMapID:
ldrb r0,[r4,#0x00]  @     B0:MAPID=MAPID(0xFF=ANY)
cmp  r0,#0xFF       @     ANY MAPID ?
beq  CheckEdition

ldr  r2,=#0x202BCF0 @FE8U Chaptor Pointer  (@ChapterData)
ldrb r1,[r2,#0xE]   @     ChapterData->MAPID
cmp  r0,r1
bne  Loop           @     �����s��v�Ȃ̂ŁA���̃��[�v�� continue;

CheckEdition:
ldrb r0,[r4,#0x01]  @     B1=Editon(0xFF=ANY,0x00=����,0x01=�G�C���[�N,0x02=�G�t����)
cmp  r0,#0xFF       @     ANY Editon ?
beq  CheckFlag

ldr  r2,=#0x202BCF0 @FE8U Chaptor Pointer  (@ChapterData)
ldrb r1,[r2,#0x1B]  @     ChapterData->Edition
cmp  r0,r1
bne  Loop           @     �����s��v�Ȃ̂ŁA���̃��[�v�� continue;



CheckFlag:
ldrh r0,[r4,#0x02]  @     W2:Flag=Flag(0x00=ANY)
cmp  r0,#0x0        @     ANY Flag ?
beq  Found

blh  0x08083DA8     @FE8U CheckFlag  Flag=r0  Result=r0:bool
cmp	r0,#0x00
beq  Loop           @     �����s��v�Ȃ̂ŁA���̃��[�v�� continue;

Found:              @�T�������f�[�^�Ƀ}�b�`�����B
                    @���[�U���w�肵���w�i�����[�h����
                    @r4 Table(current)

ldr r0,[r4,#0x04]   @     �w�i�摜
ldr r1,=0xFFFFFFFF  @     FEBuilderGBA�̓s�� �f�[�^��0���ł͍���̂Ń_�~�[�f�[�^�������
cmp r0,r1
beq load_defualt_bg

ldr r1, =0x06014000 @FE8U (OBJ_VRAM0_BITMAP )
blh 0x08012f50      @FE8U UnLZ77Decompress 

ldr r0,[r4,#0x08]   @     �p���b�g


b Exit

@�ݒ肪�Ȃ��ꍇ�́A�f�B�t�H���g�̔w�i�����[�h����.
load_defualt_bg:    @�f�B�t�H���g�̐ݒ�����[�h����

ldr r0, =0x080202B0 @FE8U �w�i�摜
ldr r0,[r0]         @     �|�C���^�Q�Ƃ��邱�ƂŁA���|�C���g�ɑς���.

ldr r1, =0x06014000 @FE8U (OBJ_VRAM0_BITMAP )
blh 0x08012f50      @FE8U UnLZ77Decompress 
ldr r0, =0x080202B8 @FE8U (�̓^�C�����o�����̑f��@PALETTE_POINTER )
ldr r0,[r0]         @     �|�C���^�Q�Ƃ��邱�ƂŁA���|�C���g�ɑς���.

Exit:
pop {r4}
ldr r3,=0x0802025C+1 @FE8U �߂�A�h���X
bx  r3

.ltorg
.align
Table: