using System;

namespace AICompanion.Overlay
{
    /// <summary>32x32 Happi face tray icon, generated from the shared icon art. No imported texture.</summary>
    public static class TrayIconImage
    {
        public const int Size = 32;

        // BGRA, top row first. The source art is Assets/_Project/Art/HappiIcon/HappiIcon-32.png.
        const string Bgra = ""
            + "AAAAAAAAAAB/AH8CAAAAAAAAAAB/W5EOXTFvKVgxbi5YMW4uVjBsL1YwbC9WMGwvVjBsL1YwbC9WMGwvVjBsL1YwbC9WMGwvVjBs"
            + "L1YwbC9WMGwvVjBsL1YwbC9YMW4uWDFuLlswbSp/W5EOAAAAAAAAAAB/AH8CAAAAAAAAAAAAAAAAfwB/AgAAAABkP3YcTCBgj0QX"
            + "VtlBFFLyQBNS9UATUfZAE1H2QBNR9kATUfZAE1H2QBNR9kATUfZAE1H2QBNR9kATUfZAE1H2QBNR9kATUfZAE1H2QBNR9kATUfZA"
            + "E1L1QRRS8kMXVtpLH16RZjt3HgAAAAB/AH8CAAAAAH8AfwIAAAAAWTFvOUcZW+JCEVb/Pg9S/z0PUf89D1H/PQ9R/z0PUf89D1H/"
            + "PQ9R/z0PUf89D1H/PQ9R/z0PUf89D1H/PQ9R/z0PUf89D1H/PQ9R/z0PUf89D1H/PQ9R/z0PUf89D1H/Pg9S/0IRVv9HGVvkWi9w"
            + "OwAAAAB/AH8CAAAAAWc4ehtHGV3lQBBW/z8RVPo/EVT9PxFU/j8RVP4/EVT+PxFU/kASVf4/ElX+PxFU/j8RVP4/EVT+PxFU/j8R"
            + "VP4/EVT+PxFU/j8RVP4/ElX+QBJV/j8RVP4/EVT+PxFU/j8RVP4/EVT9PxFU+kAQVv9HGl3naT17HQAAAAEAAAAAUCFnj0UTXv9A"
            + "Elf7QRJY/0ASWP9AElj/QBJY/0ASWP9BE1n/PQ5U/z8QV/9BE1j/QBJY/0ASWP9AElj/QBJY/0ASWP9AElj/QRNY/z8RV/88DlT/"
            + "QRNZ/0ASWP9AElj/QBJY/0ASWP9BElj/QBJX+0UTXv9PImaTAAAAAHVOnA1JGmLbQxJd/kITW/1CE1v/QhNb/0ITW/9CE1z/QxRc"
            + "/z4NWP9UMmr/SB9h/0AQWv9CE1z/QhNc/0ITW/9CE1v/QhNc/0ITXP9AEFr/SB5g/1Qya/8+DVj/QxRc/0ITXP9CE1v/QhNb/0IT"
            + "W/9CE1v9QxJd/kgaYt53VZkPYTZ/KkcZY/NDE1//QxRf/kMUX/9DFF//QxRf/0MUX/9GFmH/OAVU/6eWwP+Rfan/NQJR/0YXYv9D"
            + "FF//RBRf/0QUX/9DFF//Rhdi/zUCUf+Peab/qZnC/zgGVP9FFmH/QxRf/0MUX/9DFF//QxRf/0MUX/5DE1//Rxhj9WAzfC1fNXow"
            + "SRlm9kUUY/9FFWP+RRVj/0UVY/9FFWP/RRVj/0QUYf9GFmj/uqfl/97b8/9SK23/QhBh/0cXZf9EEmL/RBJi/0cWZf9DEGH/UClr"
            + "/93Z8f+8qub/Rxdp/0QTYf9FFWP/RRVj/0UVY/9FFWP/RRVj/kUUY/9JGWf3XzJ9M2E2gi9JGWn2RhRm/0YVZv5GFWb/RhVm/0YV"
            + "Zv9HFmf/QxFj/1Modv+mjOD/6OL//7GkyP80A1P/QxRh/0ofZv9KH2f/QxVh/zMCUv+uocX/6eP//6iO4f9UKXf/QxFi/0cWZ/9G"
            + "FWb/RhVm/0YVZv9GFWb+RhRm/0oZavdgM38yYTaCL0sabfZIFWr/SBZq/kgWav9IFmr/SBZq/0kYa/9CD2P/Yj2H/66T7/+pldP/"
            + "sabE/6KSt//CuNb/0cnk/9HK5P/DuNb/opK3/7GmxP+qldL/rpPv/2M/iP9BDmL/SRhr/0gWav9IFmr/SBZq/0gWav5IFWr/TBpu"
            + "92AzhDJhNocvTBtw9kkWbf9JF23+SRdt/0kXbf9JF23/Sxlv/0QRZf94WKL/l3/E/7Omyf/d1/H/7en//+nk/f/m4Pr/5uD6/+nk"
            + "/f/t6f//3tfx/7Snyv+Xf8T/eVmj/0QSZf9LGW//SRdt/0kXbf9JF23/SRdt/kkWbf9NG3H3ZjOJMmc2hy9OHXX2Sxdx/0sYcf5L"
            + "GHH/Sxhx/0oYcf9NG3T/QQ9m/3tklv/Wz+j/6+f+/+Da9P/e1/H/4dr1/+Hb9f/h2/X/4dr1/97X8v/g2vT/6+f+/9fQ6P99Z5f/"
            + "QQ9m/00adP9KGHH/Sxhx/0sYcf9LGHH+Sxdx/08cdPdmOIkyZzaNL08dePZMGHX/TBl1/kwZdf9MGXX/TRt1/0gTcv9YMXj/1tDn"
            + "/+nj/f/f2fT/7ur//+rl/v/h2/X/4tz2/+Lc9v/h2/X/6uX+/+7q///f2fT/6OP8/9fS6f9ZM3n/RxJy/00bdf9MGXX/TBl1/0wZ"
            + "df5MGHX/Tx1492Y4jjJnNo0vUB579k0ZeP9NGnj+TRp4/00aeP9PG3v/RBNs/7iszP/t6f//39n0/+Pe9v+Xh6X/tarG/+nk/P/h"
            + "2vX/4dr0/+nk/P+1qsb/l4il/+Pe9v/f2fT/7ej//7qvzv9FFGz/Txt6/00aeP9NGnj/TRp4/k0ZeP9QHXv3ZjiOMmc7ki9SH3/2"
            + "Txp8/08bfP5PG3z/UR19/0cQdf9zU5H/6uf8/93W8v/t6P//opSx/z4hRv5BJUr/1M3l/+Xf+f/l3/n/0crj/1c+Yf4uDzb/nY+s"
            + "/+7p///d1vH/6+j9/3VWkv9GD3X/UR19/08bfP9PG3z+Txp8/1IegPdrOJMyZzuXL1MggvZQG4D/UBx//lAbf/9THoL/RRBy/62d"
            + "w//s6P//3dbx/+/r//+EcZL/KQ8w/TcbQPy/tdD/6OP9/+fi/P/CuNT/NBg9/ywSNPqCbo//7+v//93W8f/s6P//r6DF/0URcv9T"
            + "HoL/UBx//1Acf/5QG3//UyCD92s4kzJsO5cvViCF9lIbhP9SHIP+Uh2D/1EbhP9RI3v/z8ji/+bh+//f2fP/6eT8/761z/83GUD+"
            + "Yktt/uDb8v/k3vj/5N73/+Lc9P9hSmz/OBpC/b2zzf/p5Pz/39nz/+bg+v/RyuT/UiR8/1EahP9SHYP/UhyD/lIbg/9VIIb3aziZ"
            + "Mmw7nS9XIYj2UxyH/1Mdhv5UH4b/UBeF/2A3h//e2fD/4934/+Da9f7g2vT95+H7/czF3v7Z0uz/6eT8/9nR8f/Z0fD/6eP7/9rU"
            + "7f/LxN3+5+H7/eDa9f3g2vX+4933/9/b8f9hOYj/TxeF/1Qfhv9THYb+UxyG/1YhifdrPZ4ybDudL1kijPZVHYv/VR6K/lYgiv9Q"
            + "F4j/Zj+N/+Le8//j3Pf/6eT8/+vm/v/p5Pz/7+r//+Xg+v/f2fP/1Mvq/9TL6v/f2fP/5d/5/+/q///p5Pz/6+b+/+nk/f/i3Pf/"
            + "49/0/2hBjv9PFof/ViCL/1Ueiv5VHYr/WCKN93A9njJsO6IvWSOQ9lYejv9WH43+VyCN/1MZjP9iOIv/3trw/+Te+P3YzvTdzb7y"
            + "v8u+8r3UyPLU5N74+tTM6P/FvNn/xrzZ/9TM6P/k3vj608nx1cu+8r3MvvK/2M/03OPd+P3f2/H/YzuM/1IZjP9XIY7/Vh+N/lYe"
            + "jv9ZI5H3cD2jMmw7oi9aJJT2Vx+S/1cgkf5YIZH/Vx6T/1Ulhf7Vzuf/4dr67KiM6oiihOmBpYjrg6GD6n7WzPTY6+b+/+Te+P3k"
            + "3vj96+b+/9fM9NqihOp/pYbrg6KE64Gmi+qH4dn66tfQ6P9WJ4b+Vx6T/1ghkf9XIJH+Vx+S/1oklPdwPagycTuoL1wll/ZZIJb/"
            + "WSGV/lkhlf9cI5j/TRWG/7Chxv/s6P/+2M7y4s7B88TOv/PC18302uPc9/vi3Pb/4dv1/+Hb9f/i3Pb/4933+9jN9drOwfPCzsHz"
            + "xNjO8+Hs6P/+sqPI/00Whv9cI5j/WSGV/1khlf5ZIJb/XCWX93A9qDJxQagvXSaa9lohmf9aIpj+WiKY/1wkmf9SF5P/elqe/+ro"
            + "+//m4Pr/6+b+/+vm/v/q5P3/4933/+Lc9v/i3Pb/4tz2/+Lc9v/j3ff/6uT9/+vm/v/r5v7/5uD6/+vo/P98XJ//UhaT/1wkmf9a"
            + "Ipj/WiKY/lohmf9dJpr3cD2tMnFBrS9fJ572XCKd/1wjnP5cI5z/XCOc/14kn/9SHIv/vLHQ/+rm//7e1/L94dv1/eHb9f3i3Pb/"
            + "4tz2/+Lc9v/i3Pb/4tz2/+Lc9v/h2/X94dv1/d7X8v3q5f/+vrPR/1Idi/9eI5//XCOc/1wjnP9cI5z+XCKd/2Annvd1Pa0ydD+v"
            + "MGAnofZdIqD/XSOf/l0jn/9dI5//XiWg/1kdn/9lPJL/3Njr/+fi/P/f2PP/4tz2/+Lc9v/i3Pb/4tz2/+Lc9v/i3Pb/4tz2/+Lc"
            + "9v/f2PP/5+H8/93Z7f9nPpP/WRye/14loP9dI5//XSOf/10jn/5dIqD/YSei93NBrzN5QrYqYiil818kpP9fJKP+XySj/18ko/9e"
            + "JKP/YSel/1canf9xTZr/29jr/+zn///g2fX/4Nn0/+Hb9f/h2/X/4dv1/+Hb9f/g2fT/4Nn1/+zn///c2ez/c0+a/1YZnf9hJ6X/"
            + "XiSj/18ko/9fJKP/XySj/l8kpP9hKKX1d0S1LZxi1w1mLKrcYiWq/mAlpv1gJab/YCWm/2Alpv9gJab/Yyip/1gbof9nPJb/u7DP"
            + "/+nm+f/s6P//6OL8/+Xf+v/l3/r/6OL8/+zo///p5/n/vLHQ/2g+l/9YGqH/Yyip/2Alpv9gJab/YCWm/2Alpv9gJab9YiWq/mYr"
            + "q96IVcwPAAAAAG00sZFpKbb/YSWo+2Imqv9iJqr/Yiaq/2Imqv9hJqn/ZCms/14gqv9WH5j/elik/6uaxP/Iv9v/1M7l/9TO5f/J"
            + "wNv/q5vE/3tZpP9XH5f/Xh+q/2QprP9hJqn/Yiaq/2Imqv9iJqr/Yiaq/2ElqPtpKbb/bTOxlQAAAAAAAP8Be0bBHWkvs+dmJ7T/"
            + "Yiat+mMnrv1jJ63+Yyet/mMnrf5jJ63+ZCmt/mUosf5cHar+Vhqf/lwmm/5iMZ3+YjGd/lwmm/5WGp/+XByp/mUosf5kKa3+Yyet"
            + "/mMnrf5jJ63+Yyet/mMnrf5iJq36Zie0/2ovs+l7Sr0fAAD/AX8AfwIAAAAAeUC+O20vuOVsK77/Zii1/2Unsv9lJ7L/ZSey/2Un"
            + "sv9lJ7L/ZCey/2Yqs/9nKrX/Zie2/2Qltf9kJbX/Zie2/2cqtv9mKrP/ZCey/2Unsv9lJ7L/ZSey/2Unsv9lJ7L/Zii1/2wqvv9s"
            + "MLnmd0G9PgAAAAB/f/8CAAAAAH9//wIAAAAAf0zDHnM3wJJrL7ncaSy39GkstvdpLLb3aSy292kstvdpLLb3aSy292kstvdpLbb3"
            + "aS2292kttvdpLbb3aSy292kstvdpLLb3aSy292kstvdpLLb3aSy292kst/RsL7ndcTe/lH9PxyAAAAAAf3//AgAAAAAAAAAAAAAA"
            + "AH8A/wIAAAAAAAAAAI9fzxB8RMYtgkjKMXpCxjJ6QsYyekLGMnpCxjJ6QsYyekLGMnpCxjJ6QsYyekLGMnpCxjJ6QsYyekLGMnpC"
            + "xjJ6QsYyekLGMnpCxjKCSMoxfETGLYda0hEAAAAAAAAAAH8A/wIAAAAAAAAAAA==";

        public static byte[] CreateBgra()
        {
            return Convert.FromBase64String(Bgra);
        }

        public static byte Alpha(byte[] bgra, int x, int y)
        {
            return bgra[(y * Size + x) * 4 + 3];
        }

        public static byte Red(byte[] bgra, int x, int y)
        {
            return bgra[(y * Size + x) * 4 + 2];
        }
    }
}
