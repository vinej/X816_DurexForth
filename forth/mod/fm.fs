\ FM - the YM2151 note API that AUDIOFM.TXT said was missing.
\ Cart: NEEDS FM      SD card: INCLUDE FM
\
\ Ported from the X16 ROM's audio bank by way of X816_Library
\ (src_acme/audio/ym.asm), which is the authority on this data: the 163
\ instrument patches, the drum tables and the note conversions below
\ were generated FROM that module, not typed twice.
\
\ Everything here is built on base.fs's YM! and YM@. YM@ is a shadow -
\ the chip answers no reads at all - and that shadow is what lets FMPAN
\ keep an instrument's feedback and FMINST keep the caller's panning,
\ since register $20+channel carries both.

decimal

\ ---------------------------------------------------------------------
\ THE DATA LIVES IN SDRAM, and that is not a preference.
\
\ `,` and `c,` advance HERE_PTR, which is SIXTEEN bits, with no carry
\ into HERE_BANK; only `:` calls bank_headroom, and only to guarantee
\ each definition its documented 1 KB. A 4 KB table compiled into the
\ dictionary can therefore run off its bank's ceiling and wrap to $0000
\ of the same bank, quietly overwriting whatever was at the bottom. Far
\ data has no banks to fall off.
\ ---------------------------------------------------------------------
4936 far-buffer: fm-data

fm-data value (fp)
: f, ( x -- ) (fp) ! (fp) 4 + to (fp) ;

fm-data              constant fm-carriers  ( 4 cells, one per operator )
fm-data 16 +       constant fm-midi>kc   ( MIDI note -> YM key code )
fm-data 144 +     constant fm-drumpat   ( GM drum note -> patch )
fm-data 272 +      constant fm-drumkc    ( GM drum note -> key code )
fm-data 400 +     constant fm-psglo     ( MIDI note -> PSG freq word )
fm-data 528 +     constant fm-psghi
fm-data 656 +    constant fm-patches   ( 163 patches, 26 bytes each )
fm-data 4896 +    constant fm-atten     ( our attenuation per channel )
fm-data 4904 +   constant fm-tl        ( the patch's TL, ch*4+op )

  $00000080 f, $000000e0 f, $000000f0 f, $000000ff f, $ffffffff f, $ffffffff f,
  $ffffffff f, $020100ff f, $08060504 f, $0d0c0a09 f, $1211100e f, $18161514 f,
  $1d1c1a19 f, $2221201e f, $28262524 f, $2d2c2a29 f, $3231302e f, $38363534 f,
  $3d3c3a39 f, $4241403e f, $48464544 f, $4d4c4a49 f, $5251504e f, $58565554 f,
  $5d5c5a59 f, $6261605e f, $68666564 f, $6d6c6a69 f, $7271706e f, $78767574 f,
  $7d7c7a79 f, $ffffff7e f, $ffffffff f, $ffffffff f, $ffffffff f, $ffffffff f,
  $80808080 f, $80808080 f, $80808080 f, $80808080 f, $80808080 f, $80808080 f,
  $83828180 f, $82848482 f, $86827085 f, $89888786 f, $8a8b8a88 f, $8a8d8a8c f,
  $8f8a8e8a f, $90918f90 f, $8f928e71 f, $938a9393 f, $71a1a193 f, $95947871 f,
  $85989796 f, $9a997373 f, $9d949c9b f, $a29f879e f, $80808080 f, $80808080 f,
  $80808080 f, $80808080 f, $80808080 f, $80808080 f, $80808080 f, $80808080 f,
  $80808080 f, $80808080 f, $00000000 f, $00000000 f, $00000000 f, $00000000 f,
  $00000000 f, $00000000 f, $1e3e0000 f, $75111810 f, $2e4e5e4e f, $2e41613e f,
  $355e2e2e f, $3c7e387e f, $7e447e40 f, $4538312e f, $6e082e68 f, $2e2e343a f,
  $756d6e28 f, $3e1e7e6e f, $681a1a3e f, $557e4e55 f, $446e7e7e f, $3e3e6525 f,
  $00000000 f, $00000000 f, $00000000 f, $00000000 f, $00000000 f, $00000000 f,
  $00000000 f, $00000000 f, $00000000 f, $00000000 f, $1a181715 f, $201f1d1b f,
  $29272422 f, $34312e2b f, $413e3a37 f, $524e4945 f, $68625d57 f, $837c756e f,
  $a59c938b f, $d0c5baaf f, $07f8eadd f, $4b382716 f, $a18a745f f, $0ef0d4ba f,
  $96714e2d f, $4314e8be f, $1ce1a974 f, $2de39d5a f, $8628d07c f, $38c252e9 f,
  $5bc63ab5 f, $0c51a0f9 f, $7184a5d3 f, $b78d746b f, $19a240f2 f, $e2094ba6 f,
  $6e1ae8d6 f, $324480e4 f, $c413974d f, $dc35d1ad f, $658901c9 f, $88262e9a f,
  $00000000 f, $00000000 f, $00000000 f, $00000000 f, $00000000 f, $00000000 f,
  $00000000 f, $00000000 f, $00000000 f, $00000000 f, $01000000 f, $01010101 f,
  $01010101 f, $02010101 f, $02020202 f, $03030202 f, $04030303 f, $05040404 f,
  $06060505 f, $08070706 f, $0a090908 f, $0d0c0b0a f, $100f0e0d f, $14131211 f,
  $1a181715 f, $201f1d1b f, $29272422 f, $34312e2b f, $413e3a37 f, $524e4945 f,
  $68625d57 f, $837c756e f, $515900c4 f, $18350101 f, $1f1f0017 f, $01159f9f f,
  $00000809 f, $51330000 f, $00c4f6f5 f, $01015359 f, $00171835 f, $9f9f1f1f f,
  $08090115 f, $00000000 f, $f6f55133 f, $661100f9 f, $2b216144 f, $1f1f002f f,
  $04081f1f f, $0000090a f, $f1f30000 f, $00f4f7f4 f, $61017311 f, $000d2c19 f,
  $9f1f1f9f f, $1e0f0007 f, $0b0e070e f, $06070714 f, $515e00c4 f, $35390101 f,
  $1f1f0017 f, $01151f1f f, $00000809 f, $51330000 f, $00fcf8f8 f, $313c313b f,
  $001e2126 f, $1f1f1f1f f, $080f0600 f, $00000000 f, $f8f7b1f3 f, $363b00c0 f,
  $1f0d3135 f, $1f1f0016 f, $05181f1f f, $0000091c f, $11670006 f, $00f8f916 f,
  $31313231 f, $00161320 f, $1f1f1f1f f, $09000017 f, $00000008 f, $faf4b143 f,
  $3a5600c4 f, $3c283136 f, $1f1f000c f, $0b1a1f1f f, $00000b1a f, $b1fb0000 f,
  $00ccf5fb f, $01005a5a f, $007f187f f, $1f1f1f1f f, $0f04110d f, $0a000000 f,
  $45f4f1f3 f, $515500fc f, $51160101 f, $1f1f020e f, $130f1f1f f, $00001711 f,
  $b4f60000 f, $03c4fdfc f, $31343535 f, $040a502d f, $1f1f1f1f f, $8a0d0a0e f,
  $00100006 f, $f8fbb1ab f, $515800fd f, $111e0108 f, $1f1f0218 f, $0c141f1f f,
  $00000f1a f, $f6d30000 f, $00e5f9fd f, $01095158 f, $0b1b0014 f, $1f1f1f1f f,
  $0f1a0f16 f, $00000000 f, $fbfdfbdd f, $3e1900d4 f, $20103262 f, $9f9f0e0e f,
  $07049f9f f, $00000807 f, $e1f10000 f, $00c0e3f4 f, $31333133 f, $021b2621 f,
  $1f1f1f1f f, $09070411 f, $00000009 f, $f5f3b523 f, $323860c7 f, $0b163134 f,
  $1f1f040e f, $0d0d1f1f f, $00000d0d f, $1b1b0000 f, $60c71b1b f, $31333236 f,
  $050d0910 f, $1f1f1f1f f, $100d1110 f, $00000000 f, $2b3b3b5b f, $415d60c2 f,
  $23363102 f, $1f17071e f, $001f1f1f f, $00000000 f, $b1fe0000 f, $00c4fbf4 f,
  $54511411 f, $0d0b1e22 f, $1f1f1f1f f, $09098909 f, $00000000 f, $06060000 f,
  $333300fc f, $21213131 f, $1f1f120c f, $00001f1f f, $00001818 f, $45450000 f,
  $00d40707 f, $01724144 f, $06130c12 f, $10101f1f f, $0a0a0000 f, $00000000 f,
  $1b1b0103 f, $313200f8 f, $16323132 f, $1f1f021b f, $0000101f f, $00000c00 f,
  $06060000 f, $00e41b06 f, $31323134 f, $060b101d f, $10101f1f f, $0a0a0000 f,
  $00000000 f, $1b1b0103 f, $626800c2 f, $262d6165 f, $1f190028 f, $071c1f1f f,
  $00000a08 f, $f1ff0000 f, $00c2f9f4 f, $61636168 f, $00182827 f, $1f1f1f19 f,
  $0a07051c f, $00000000 f, $f9f491ff f, $313100fa f, $2b243124 f, $1f17002d f,
  $00151f1f f, $00000a00 f, $f1ff0000 f, $00f8f9f4 f, $31213129 f, $000f1e2e f,
  $1f1f1f1f f, $09080410 f, $00000000 f, $fbf4b1f9 f, $515400e2 f, $21200101 f,
  $1f1c0032 f, $02151f1f f, $00000a03 f, $b1ff0000 f, $00faf9f4 f, $33321133 f,
  $00211610 f, $1f1f1f1f f, $07000017 f, $00000000 f, $fbf4b1ff f, $113300fa f,
  $0b093331 f, $1f1f001a f, $00171f1f f, $00000700 f, $b1ff0000 f, $00fafbf4 f,
  $6a383462 f, $0a211610 f, $1f1f1f1f f, $06000017 f, $00000000 f, $fbf4b1ff f,
  $515200e0 f, $193b0102 f, $1f1f000f f, $0c0f1f1f f, $00000916 f, $6e5e0000 f,
  $00c0fe6e f, $61626163 f, $002e181f f, $1f1f1f1f f, $040d0a0f f, $00000000 f,
  $fb1a1a1b f, $616500c0 f, $21186163 f, $1f1f0021 f, $0a121f1f f, $03010412 f,
  $1a3b0002 f, $00c0fb1a f, $31312121 f, $003c1a15 f, $1f1f1f1f f, $04000000 f,
  $00000000 f, $fcf6b7f3 f, $616900c8 f, $111a6165 f, $1f1f0029 f, $07161f1f f,
  $0707080a f, $13930807 f, $00db8813 f, $61656169 f, $00251107 f, $1f1f1f1f f,
  $080a0913 f, $08070407 f, $88134383 f, $710100f9 f, $11323171 f, $1f1f050a f,
  $06131f1f f, $0000001d f, $22a20000 f, $00f968e2 f, $31313131 f, $05300e28 f,
  $1f1f1f1f f, $001d0813 f, $00000000 f, $68e22272 f, $025860c3 f, $132d4241 f,
  $1f1f051a f, $0e000e1f f, $00000000 f, $60000000 f, $60c30a00 f, $42410258 f,
  $051a133b f, $0e1f1f1f f, $00000e00 f, $00000000 f, $0a006000 f, $545863c3 f,
  $383d0201 f, $1f1f021a f, $00000f1f f, $00000000 f, $00000000 f, $43c30900 f,
  $01015455 f, $021a3a3a f, $111f1f1f f, $00000000 f, $00000000 f, $0a000000 f,
  $135103fc f, $1b1c1151 f, $1413090b f, $0b090e10 f, $0404908b f, $17170404 f,
  $00c22717 f, $31135235 f, $002c1c2a f, $5f1f1f9f f, $12141517 f, $0d0a0005 f,
  $3627a769 f, $323500c2 f, $312e3133 f, $1f9f002c f, $15179f1f f, $00050f14 f,
  $a969060a f, $00dd3327 f, $61612110 f, $02040027 f, $1f1f1f1f f, $0a0a0a00 f,
  $0f0f0fd0 f, $1a2a0b0a f, $323130ec f, $3c0b1171 f, $15150806 f, $00050f0e f,
  $00000600 f, $01610400 f, $30ec0806 f, $01713231 f, $0402310d f, $110d1215 f,
  $06000004 f, $04000000 f, $06060202 f, $313130fc f, $1b1f1151 f, $14130100 f,
  $00040e10 f, $0400100b f, $17340400 f, $30fc2706 f, $14511151 f, $0305181c f,
  $0e101413 f, $100b0004 f, $00000400 f, $27171707 f, $303160c4 f, $27183031 f,
  $1f1f0a03 f, $00000b0b f, $00000000 f, $0f000000 f, $00e60606 f, $31313238 f,
  $00051a1d f, $1f1f1f1f f, $0d000017 f, $00000000 f, $770877fc f, $303160c4 f,
  $25253031 f, $1f1f0105 f, $00000b0b f, $00000000 f, $0f0f0000 f, $00fc0607 f,
  $62316202 f, $0000000b f, $1f1f151f f, $0f0e1400 f, $00000000 f, $faf8fc00 f,
  $313160fc f, $1d1d3131 f, $14140800 f, $06061313 f, $00000606 f, $68680000 f,
  $00f50808 f, $31543131 f, $0035001a f, $18161914 f, $08191505 f, $00000000 f,
  $27291911 f, $613100fd f, $001d6131 f, $0e0d0000 f, $1f1f0f10 f, $000b1f1f f,
  $08070000 f, $50fc0809 f, $31313631 f, $0b0a1d23 f, $13131414 f, $06060606 f,
  $00000000 f, $08086868 f, $313100f4 f, $22253131 f, $5f1f0000 f, $0f051510 f,
  $00000909 f, $25410000 f, $00f54747 f, $34023131 f, $0f260217 f, $9191949f f,
  $0005000b f, $02020200 f, $08361713 f, $516100fa f, $2a2a0161 f, $139c0010 f,
  $0408134f f, $0000060a f, $b0030006 f, $02fd3824 f, $02016101 f, $0a070020 f,
  $1f1f1f13 f, $00000808 f, $00001f00 f, $0809f82a f, $345160fd f, $191a3101 f,
  $18170018 f, $0000131f f, $00000000 f, $4bf00000 f, $60fdf9f6 f, $31013451 f,
  $00181917 f, $120f0d17 f, $00000000 f, $00000000 f, $f9f64bf0 f, $345160fd f,
  $191d3101 f, $141a0018 f, $00001419 f, $00000000 f, $4bf00000 f, $00ebf9f6 f,
  $31313231 f, $00121b0f f, $131f131f f, $08000900 f, $00000017 f, $08040453 f,
  $313150dc f, $12123131 f, $1f1f0a00 f, $00001d16 f, $00000007 f, $00000700 f,
  $40ec0808 f, $32313131 f, $0000241c f, $16121f1f f, $00070000 f, $07000000 f,
  $08080000 f, $545400c3 f, $383d0201 f, $1414001a f, $00001114 f, $00000000 f,
  $01000000 f, $50c20a00 f, $31343231 f, $0634262e f, $131f1305 f, $08000900 f,
  $00000017 f, $08000c5f f, $313100cc f, $20293131 f, $1f1f0e09 f, $04051413 f,
  $02010304 f, $46320a06 f, $50cc7949 f, $31313131 f, $0e092029 f, $120d1f1f f,
  $03040405 f, $00010301 f, $77494632 f, $313100cf f, $11083131 f, $18170d11 f,
  $03031617 f, $00000303 f, $2a2a0000 f, $00ec2a2a f, $31023433 f, $000a2100 f,
  $12931314 f, $12101009 f, $00000000 f, $28f929f0 f, $323300ec f, $21003102 f,
  $1314000a f, $10091190 f, $00001210 f, $f9f00000 f, $00ec08f9 f, $3102323f f,
  $000b2100 f, $1b93131f f, $121f1000 f, $00000000 f, $088959f0 f, $412162c7 f,
  $00004121 f, $04500e00 f, $8c0c0e0a f, $03088b0c f, $1b0d0003 f, $00c70b1d f,
  $30303031 f, $7f7f7f08 f, $1f1f1f14 f, $1f1f1f03 f, $1f1f1f00 f, $ffffff38 f,
  $323240fb f, $251b3132 f, $1f1f021d f, $0909191f f, $00000009 f, $00000000 f,
  $50fb0900 f, $31313131 f, $001c281b f, $141f1f1f f, $00090909 f, $00000000 f,
  $09000000 f, $323250f1 f, $3c183132 f, $1f1f0021 f, $0000191f f, $00000000 f,
  $00000000 f, $50fe0a00 f, $30316130 f, $7f00000b f, $1f131b1f f, $1f100000 f,
  $1f070017 f, $fffd0a50 f, $363150cb f, $0b0f3831 f, $0d17000a f, $09081f11 f,
  $0000090e f, $80f00000 f, $50c46950 f, $31313131 f, $07001818 f, $14141f1f f,
  $00000000 f, $00000000 f, $0b090f00 f, $060450d4 f, $271d6362 f, $4e5f0506 f,
  $0a0a1d1f f, $02020505 f, $00500202 f, $50c52808 f, $31323132 f, $1d00070d f,
  $151c165f f, $0505050d f, $070c0701 f, $38474b53 f, $515e10c4 f, $102a0101 f,
  $041f0202 f, $04080a1f f, $0000050e f, $b3f30000 f, $10c2f4f7 f, $01015155 f,
  $0216107f f, $0a080403 f, $05000408 f, $00000000 f, $f4f4b3f3 f, $313110fc f,
  $1c1c3131 f, $091f0000 f, $0007071f f, $00000009 f, $09500000 f, $10c40808 f,
  $30313031 f, $130a2525 f, $0b0b1f1f f, $00000000 f, $00000000 f, $07060000 f,
  $393100c6 f, $0e22313a f, $08100f06 f, $09090a06 f, $00000907 f, $39010000 f,
  $00c43879 f, $01015159 f, $00171835 f, $878b1f1f f, $08090115 f, $00000000 f,
  $f5f85133 f, $313350c4 f, $25253031 f, $1f1f130a f, $00000b0b f, $00000000 f,
  $00000000 f, $61c40706 f, $62610402 f, $0a0a1414 f, $0e104545 f, $05058a8a f,
  $02020202 f, $27070809 f, $515500e1 f, $28110101 f, $1f1f0012 f, $17181e1f f,
  $00001300 f, $f1f30b00 f, $10c4b7f4 f, $63620604 f, $0a001423 f, $09174e5f f,
  $05050a0a f, $02020202 f, $28080000 f, $343100c4 f, $17293231 f, $1f1f0e08 f,
  $09001f1f f, $00000910 f, $26030001 f, $50c4f484 f, $01015151 f, $0202102a f,
  $0a1f041f f, $050e0808 f, $00000000 f, $f6f7b0f0 f, $626140ff f, $0d0b0301 f,
  $1f1f100d f, $0f171f1f f, $00000d0a f, $ba1c0000 f, $00c4b7c8 f, $31313531 f,
  $001d2729 f, $15091f1f f, $09000900 f, $00c10000 f, $f48b2603 f, $515300c4 f,
  $282b0101 f, $1f1f0012 f, $17001e07 f, $0000121f f, $f1f30b00 f, $51d5b9ff f,
  $31313244 f, $0a05111d f, $151f1f17 f, $09100a86 f, $0e130000 f, $88674620 f,
  $323100f6 f, $000c3431 f, $0d170c00 f, $0d101f1c f, $1f061109 f, $f7200008 f,
  $00c1b8c8 f, $31313231 f, $001e152a f, $1f1f1f1f f, $110e0909 f, $0b000000 f,
  $6995f505 f, $615100ea f, $0d0e2369 f, $9f9e0007 f, $02179fdf f, $03000d1a f,
  $2b3a0b08 f, $00c2689a f, $31135235 f, $001e2113 f, $5f1f1f9f f, $0b140017 f,
  $0a0a0005 f, $3524b565 f, $515801fd f, $11220208 f, $1f1f020d f, $12141f1f f,
  $00000f1a f, $f6d30000 f, $00ebf9fd f, $31313331 f, $001d1b28 f, $131f131f f,
  $08000900 f, $00000017 f, $0b000c50 f, $623151fa f, $29263135 f, $51570017 f,
  $0d8a4e4e f, $0000040b f, $26150000 f, $50fa0958 f, $32313231 f, $00141f1f f,
  $11111416 f, $00000000 f, $1f1f001f f, $99f5b5f7 f, $640500c7 f, $11006238 f,
  $121f0a0a f, $0d0e121f f, $1f1f0b0d f, $f6f61f13 f, $00fcf6f6 f, $00005c50 f,
  $0000000f f, $181f1f1f f, $1f110000 f, $111200c0 f, $0af8f3f2 f, $635500e0 f,
  $212a3105 f, $101f0047 f, $0b081f10 f, $0a000a0d f, $37f30000 f, $00fcf7f9 f,
  $303f303f f, $00002a10 f, $1f1f1f1f f, $12170000 f, $80000000 f, $f6fc0606 f,
  $303f00fc f, $270f3030 f, $1f1f0000 f, $00001f1f f, $00001312 f, $06068000 f,
  $00e0f6f6 f, $01015255 f, $00001a35 f, $181f1f1f f, $0e1b1517 f, $00000000 f,
  $f9f4b1f3 f, $303300fc f, $230b3030 f, $1f1f0000 f, $00121f1f f, $00001212 f,
  $06f00000 f, $00f8f6f8 f, $303f3f3f f, $00000000 f, $051f1f1f f, $1f000000 f,
  $0000001f f, $ff0607f8 f, $326200fa f, $16006a38 f, $1f1f0021 f, $0017141f f,
  $40400f00 f, $b14f4080 f, $00ecfbf4 f, $31023233 f, $020a2100 f, $12531314 f,
  $16101009 f, $00000000 f, $fff9f9f0 f, $3f3f30f8 f, $0000303f f, $1f1f0000 f,
  $0000021f f, $001f0700 f, $00f00000 f, $00c2f400 f, $32323933 f, $00181847 f,
  $12161215 f, $90101505 f, $800f8005 f, $ff0f6f4f f, $313100c0 f, $06353b31 f,
  $1f1f0610 f, $06091f1f f, $80400608 f, $0f0f0880 f, $00f80f0f f, $3b313131 f,
  $06100617 f, $061f1f1f f, $00080609 f, $00808040 f, $04000000 f, $3f3f01f8 f,
  $0000303f f, $1f1f0000 f, $00000a1f f, $001f8700 f, $00f00000 f, $00f8f400 f,
  $60000000 f, $00000000 f, $5f1f1f1f f, $09000000 f, $0e000000 f, $08505060 f,
  $000000e7 f, $7f7f0000 f, $1f1f7f7f f, $1f1f1f1f f, $1f1f1f1f f, $ffff1f1f f,
  $00f8ffff f, $60000000 f, $00000000 f, $5f1f1f1f f, $0d000000 f, $00000000 f,
  $38505060 f, $505200fb f, $16080f00 f, $1f1f0011 f, $00001f1f f, $c0801500 f,
  $ffff0040 f, $00c0ffff f, $00005050 f, $00270000 f, $1f1f1f1f f, $0e161118 f,
  $004ec080 f, $f8faf8fa f, $525500e8 f, $2d2c0101 f, $141a0200 f, $15171114 f,
  $0000101b f, $b1f30000 f, $00fbfbf4 f, $31323232 f, $021d251b f, $1f1f1f1f f,
  $16090909 f, $00000000 f, $f9000000 f, $505000fd f, $00110000 f, $1f1f0200 f,
  $00181f1f f, $13000000 f, $08ff190d f, $00fb0809 f, $0f005052 f, $00222808 f,
  $1f1f1f1f f, $12000000 f, $0040c080 f, $f8f4f4f4 f, $515f00fc f, $1000000f f,
  $1f1f0000 f, $1a1f1f1f f, $00000e11 f, $f1050a00 f, $00fb7af8 f, $0f005052 f,
  $001a1608 f, $1f1f1f1f f, $11000000 f, $0f40c080 f, $68f7f7f7 f, $313000d4 f,
  $13003034 f, $1fd90000 f, $13129fdf f, $0a0a0d11 f, $f5f30200 f, $00f6c6f6 f,
  $393f383f f, $53006500 f, $1f180016 f, $0b160c0f f, $00120000 f, $46294905 f,
  $383f00f6 f, $2000393f f, $00167c07 f, $0c0f1f16 f, $00000b0f f, $4f030018 f,
  $70f64f5b f, $3030383f f, $7f047f00 f, $1f180016 f, $180a0c0f f, $000b0000 f,
  $ff5f4f06 f, $505000c0 f, $00000f00 f, $1f1f0000 f, $051a181f f, $c0c00003 f,
  $f0f00a40 f, $00c006f0 f, $3f363732 f, $060c1e54 f, $1f1f1f1f f, $0c000000 f,
  $00c000c0 f, $f6000000 f, $505000c0 f, $03000000 f, $1f1f0400 f, $051a181f f,
  $40c00003 f, $00f00a40 f, $00fc0690 f, $4b682f0b f, $00000000 f, $151fdf1f f,
  $11110000 f, $0000c01f f, $feff00ff f, $000000f8 f, $00306000 f, $1f1f0000 f,
  $00005f1f f, $00000c00 f, $50600d00 f, $00d03750 f, $303c3230 f, $002e1b0c f,
  $1fdf1fd8 f, $0b11140e f, $0e000a0a f, $27f7f3f3 f, $326200fa f, $16006a38 f,
  $1f1f0721 f, $0000121f f, $40401100 f, $b1404b80 f, $00c7b8f4 f, $05764729 f,
  $0e000000 f, $1212160f f, $0b0b0b0b f, $d9591919 f, $1b1d1e1d f, $472900c7 f,
  $00000576 f, $160f0e00 f, $05051212 f, $1f1f0505 f, $1e1ddf5f f, $00f81b1d f,
  $307c0000 f, $0232081e f, $5f151f1f f, $0e000000 f, $1f080000 f, $1fff5f6f f,
  $000000f8 f, $081e307c f, $1f1f0232 f, $00004c0b f, $00000a00 f, $5f6f1f08 f,
  $00c01fff f, $30303730 f, $00340007 f, $1f1f1f1f f, $10161118 f, $1d0e0000 f,
  $6fffffff f, $373000c0 f, $00073030 f, $1f1f0034 f, $05181f1f f, $00000d0d f,
  $bfff1d0e f, $00cc6fdf f, $31303a3a f, $007f187f f, $1f1f1f1f f, $0c04110d f,
  $1f000000 f, $1ff4f1f3 f, $3a3a00cc f, $187f3130 f, $1f1f007f f, $110d1f1f f,
  $00000c04 f, $f1f30800 f, $00c715f4 f, $05764729 f, $02000000 f, $121f121f f,
  $14140d0d f, $d34f9410 f, $1b1d1e1d f, $4d2b00c7 f, $00000d79 f, $1f1f0200 f,
  $13141f1f f, $4f8d1314 f, $9e6d8d4f f, $00c46d4d f, $00005170 f, $00261030 f,
  $1f1f1f1f f, $16191a19 f, $0f590080 f, $0a8cffff f, $313100c7 f, $7f7f3131 f,
  $1f1f007f f, $1f1f1f1f f, $1f1f001f f, $ffff001f f, $00c00fff f, $30303730 f,
  $004a0407 f, $1f1f181f f, $0d000118 f, $170e0000 f, $58d5b6f8 f, $517000c4 f,
  $10300000 f, $1f1f0000 f, $1a191f1f f, $00800e0c f, $f1f50a40 f, $000077f8 f,

\ ---------------------------------------------------------------------
\ Total level is volume on a CARRIER and timbre on a modulator, so
\ attenuating the wrong operator changes the sound instead of the
\ loudness. Which operators are carriers depends on the connection
\ algorithm, the low three bits of register $20+channel.
\ ---------------------------------------------------------------------
0 value fm-ch

: fm-alg ( ch -- alg ) $20 + ym@ 7 and ;
: fm-carrier? ( op alg -- flag )
  swap 4 * fm-carriers + @ swap rshift 1 and 0<> ;

\ Re-send the four total levels, each pushed down by the channel's
\ attenuation and pinned at silence rather than wrapped round to loud.
: fm-apply ( ch -- )
  7 and to fm-ch
  fm-ch fm-alg                          ( alg )
  4 0 do
    i over fm-carrier? if
      fm-ch 4 * i + fm-tl + c@
      fm-ch fm-atten + c@ +
      dup 127 > if drop 127 then
      fm-ch i 8 * + $60 + ym!
    then
  loop drop ;

\ ---------------------------------------------------------------------
\ Notes. A BASIC note is (octave<<4) | 1..12, where 1 is C; MIDI is the
\ usual 0-127 with 60 = middle C. Notes below the chip's range have no
\ key code, and the table says so with $FF.
\ ---------------------------------------------------------------------
: bas>midi ( note -- midi | -1 )
  dup 15 and dup 1 < over 12 > or if 2drop -1 exit then
  swap 4 rshift 1+ 12 * + 1- ;

: midi>kc ( midi -- kc | -1 )
  dup 0 < over 127 > or if drop -1 exit then
  fm-midi>kc + c@ dup 255 = if drop -1 then ;

: fm-keyoff ( ch -- ) 7 and dup $08 ym! drop ;
: fm-keyon  ( ch -- ) 7 and $78 or $08 ym! ;

\ ---------------------------------------------------------------------
\ FMINST - load one of the 163 built-in instruments.
\
\ Byte 0 is RL/FB/CON and shares its register with the channel's stereo
\ bits, so those are read back out of the shadow and put on again.
\ Byte 1 is PMS/AMS at $38+channel - it is not a spare, and dropping it
\ costs the instrument its vibrato and tremolo depth. Bytes 2..25 walk
\ $40+channel in steps of 8.
\ ---------------------------------------------------------------------
: fminst ( inst channel -- )
  7 and to fm-ch
  dup 0 < over 162 > or if drop 128 then    \ out of range plays Silent
  26 * fm-patches +                         ( addr )
  dup c@ 63 and                             ( addr fbcon )
  fm-ch $20 + ym@ 192 and or
  fm-ch $20 + ym!
  25 0 do
    dup i 1+ + c@
    fm-ch $38 + i 8 * + ym!
  loop
  4 0 do                                    \ keep the TLs for FMVOL
    dup i 6 + + c@ 127 and
    fm-ch 4 * i + fm-tl + c!
  loop
  drop
  fm-ch fm-apply ;

\ ---------------------------------------------------------------------
\ FMINIT - silence everything and give every channel an instrument, so
\ that FMNOTE on a fresh machine makes a sound rather than nothing.
\ ---------------------------------------------------------------------
: fminit ( -- )
  0 1 ym!                                   \ LFO reset, and a chip poke
  8 0 do i $08 ym! loop                     \ key off every channel
  32 0 do 15 i $e0 + ym! loop               \ release everything sounding
  8 0 do 0 i fm-atten + c! loop
  32 0 do 0 i fm-tl + c! loop
  8 0 do 192 i $20 + ym! loop               \ both speakers
  8 0 do 0 i fminst loop ;

\ ---------------------------------------------------------------------
\ FMNOTE - play a packed note. 0 releases: the envelope's release rate
\ finishes it, which is a release rather than a cut.
\
\ The key-off before the key code is not ceremony. The chip restarts an
\ envelope only on a fresh key-on, so retriggering a still-sounding note
\ without it changes the pitch and never re-attacks - a glide.
\ ---------------------------------------------------------------------
: fmnote ( note channel -- )
  7 and to fm-ch
  dup 0= if drop fm-ch fm-keyoff exit then
  bas>midi dup 0 < if drop exit then
  midi>kc dup 0 < if drop exit then         ( kc )
  0 fm-ch $30 + ym!                         \ no pitch bend
  fm-ch $28 + ym!
  fm-ch fm-keyoff
  fm-ch fm-keyon ;

: fmmidi ( midinote channel -- )            \ the same thing in MIDI
  7 and to fm-ch
  dup 0= if drop fm-ch fm-keyoff exit then
  midi>kc dup 0 < if drop exit then
  0 fm-ch $30 + ym!
  fm-ch $28 + ym!
  fm-ch fm-keyoff
  fm-ch fm-keyon ;

\ ---------------------------------------------------------------------
\ FMDRUM - a percussion note. Each drum is a whole instrument plus a
\ fixed key code, so this costs a full patch load; that is why it is a
\ word of its own and not a note on some percussion channel.
\ ---------------------------------------------------------------------
0 value fm-dnote
: fmdrum ( drum channel -- )
  7 and to fm-ch
  127 and to fm-dnote
  fm-dnote fm-drumpat + c@ fm-ch fminst
  0 fm-ch $30 + ym!
  fm-dnote fm-drumkc + c@ fm-ch $28 + ym!
  fm-ch fm-keyoff
  fm-ch fm-keyon ;

\ ---------------------------------------------------------------------
\ FMVOL - 0 quietest, 63 loudest, to match PSGVOL. The chip works the
\ other way round in units of 0.75 dB of attenuation, so 63 becomes 0.
\ FMPAN - 1 left, 2 right, 3 both, keeping feedback and algorithm.
\ ---------------------------------------------------------------------
: fmvol ( vol channel -- )
  7 and to fm-ch
  63 and 63 swap - 2 *
  fm-ch fm-atten + c!
  fm-ch fm-apply ;

: fmpan ( pan channel -- )
  7 and to fm-ch
  3 and 6 lshift
  fm-ch $20 + ym@ 63 and or
  fm-ch $20 + ym! ;

\ ---------------------------------------------------------------------
\ FMVIB - the chip's one LFO, shared by all eight channels. Speed is
\ the LFO frequency 0-255; depth 0-127 is how far it bends the pitch.
\ Depth also has to open each channel's PMS, or the LFO is running and
\ inaudible - so a non-zero depth sets PMS to maximum on every channel
\ and zero closes them again. The instrument's own AMS is left alone.
\ ---------------------------------------------------------------------
: fmvib ( speed depth -- )
  dup 127 and 128 or $19 ym!                ( speed depth )
  0<> if 7 else 0 then                      ( speed pms )
  8 0 do
    dup 4 lshift
    i $38 + ym@ 3 and or
    i $38 + ym!
  loop drop
  255 and $18 ym! ;

\ ---------------------------------------------------------------------
\ PSGNOTE - the same packed note on a VERA PSG voice. Frequency only:
\ set the volume with PSGVOL and the waveform with PSGWAV first, and
\ mind PSGPW - the pulse width PSGINIT leaves is a 1-in-64 duty cycle,
\ a click rather than a tone.
\ ---------------------------------------------------------------------
: psgnote ( note voice -- )
  15 and >r
  dup 0= if drop 0 r> psgvol exit then
  bas>midi dup 0 < if drop r> drop exit then
  dup fm-psglo + c@ swap fm-psghi + c@ 8 lshift or
  r> psgfreq ;
