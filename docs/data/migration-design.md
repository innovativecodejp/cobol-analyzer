# COBOL 移行設計書

生成日: 2026-07-26
対象プログラム数: 31

---

## 移行優先度ランキング

| 順位 | プログラム名 | ファイル名 | MDI | リスク | ファンイン | ファンアウト | 推奨戦略 |
|------|------------|-----------|-----|--------|----------|------------|--------|
| 1 | COACTUPC | COACTUPC.cbl | 24.3 | Low | 0 | 1 | BigBang |
| 2 | CBSTM03B | CBSTM03B.CBL | 19.5 | Low | 1 | 0 | BigBang |
| 3 | COCRDUPC | COCRDUPC.cbl | 15.3 | Low | 0 | 0 | BigBang |
| 4 | COTRN02C | COTRN02C.cbl | 12.4 | Low | 0 | 1 | BigBang |
| 5 | COCRDLIC | COCRDLIC.cbl | 11.2 | Low | 0 | 0 | BigBang |
| 6 | COUSR00C | COUSR00C.cbl | 9.7 | Low | 0 | 0 | BigBang |
| 7 | COTRN00C | COTRN00C.cbl | 9.7 | Low | 0 | 0 | BigBang |
| 8 | COCRDSLC | COCRDSLC.cbl | 9.6 | Low | 0 | 0 | BigBang |
| 9 | COACTVWC | COACTVWC.cbl | 9.5 | Low | 0 | 0 | BigBang |
| 10 | COUSR02C | COUSR02C.cbl | 9.4 | Low | 0 | 0 | BigBang |
| 11 | COUSR03C | COUSR03C.cbl | 9.4 | Low | 0 | 0 | BigBang |
| 12 | COTRN01C | COTRN01C.cbl | 9.3 | Low | 0 | 0 | BigBang |
| 13 | COBIL00C | COBIL00C.cbl | 9.2 | Low | 0 | 0 | BigBang |
| 14 | CBACT01C | CBACT01C.cbl | 8.8 | Low | 0 | 2 | BigBang |
| 15 | CORPT00C | CORPT00C.cbl | 8.6 | Low | 0 | 0 | BigBang |
| 16 | CBACT03C | CBACT03C.cbl | 8.4 | Low | 0 | 1 | BigBang |
| 17 | CBTRN03C | CBTRN03C.cbl | 8.3 | Low | 0 | 1 | BigBang |
| 18 | CBACT02C | CBACT02C.cbl | 8.2 | Low | 0 | 1 | BigBang |
| 19 | CBCUS01C | CBCUS01C.cbl | 7.9 | Low | 0 | 1 | BigBang |
| 20 | CBACT04C | CBACT04C.cbl | 7.7 | Low | 0 | 1 | BigBang |
| 21 | COADM01C | COADM01C.cbl | 7.7 | Low | 0 | 0 | BigBang |
| 22 | COUSR01C | COUSR01C.cbl | 7.6 | Low | 0 | 0 | BigBang |
| 23 | COMEN01C | COMEN01C.cbl | 7.5 | Low | 0 | 0 | BigBang |
| 24 | CBTRN02C | CBTRN02C.cbl | 7.3 | Low | 0 | 1 | BigBang |
| 25 | CBSTM03A | CBSTM03A.CBL | 7.2 | Low | 0 | 2 | BigBang |
| 26 | CBTRN01C | CBTRN01C.cbl | 7.1 | Low | 0 | 1 | BigBang |
| 27 | CBEXPORT | CBEXPORT.cbl | 7.0 | Low | 0 | 1 | BigBang |
| 28 | CBIMPORT | CBIMPORT.cbl | 6.0 | Low | 0 | 1 | BigBang |
| 29 | COSGN00C | COSGN00C.cbl | 5.7 | Low | 0 | 0 | BigBang |
| 30 | CSUTLDTC | CSUTLDTC.cbl | 4.9 | Low | 2 | 1 | Incremental |
| 31 | COBSWAIT | COBSWAIT.cbl | 0.5 | Low | 0 | 0 | BigBang |

## プログラム間依存関係

- 総プログラム数: 34
- CALL エッジ数: 16
- 循環依存: なし
- 動的CALL（解析不能）: なし

### 依存関係一覧

| 呼び出し元 | 呼び出し先 | CALL箇所数 |
|-----------|-----------|-----------|
| CBACT01C | CEE3ABD | 1 |
| CBACT01C | COBDATFT | 1 |
| CBACT02C | CEE3ABD | 1 |
| CBACT03C | CEE3ABD | 1 |
| CBACT04C | CEE3ABD | 1 |
| CBCUS01C | CEE3ABD | 1 |
| CBEXPORT | CEE3ABD | 1 |
| CBIMPORT | CEE3ABD | 1 |
| CBSTM03A | CBSTM03B | 13 |
| CBSTM03A | CEE3ABD | 1 |
| CBTRN01C | CEE3ABD | 1 |
| CBTRN02C | CEE3ABD | 1 |
| CBTRN03C | CEE3ABD | 1 |
| COACTUPC | CSUTLDTC | 1 |
| COTRN02C | CSUTLDTC | 2 |
| CSUTLDTC | CEEDAYS | 1 |

## 各プログラム分析サマリー

### COACTUPC
- **ファイル**: COACTUPC.cbl
- **MDI**: 24.3（Low）
- **推奨戦略**: BigBang
- **行数**: 4237 / **パラグラフ数**: 101
- **主要指標**: CC=2, GD=0.062, AD=0, ND=3

---

### CBSTM03B
- **ファイル**: CBSTM03B.CBL
- **MDI**: 19.5（Low）
- **推奨戦略**: BigBang
- **行数**: 231 / **パラグラフ数**: 14
- **主要指標**: CC=2, GD=0.250, AD=0, ND=1

---

### COCRDUPC
- **ファイル**: COCRDUPC.cbl
- **MDI**: 15.3（Low）
- **推奨戦略**: BigBang
- **行数**: 1561 / **パラグラフ数**: 47
- **主要指標**: CC=2, GD=0.053, AD=0, ND=2

---

### COTRN02C
- **ファイル**: COTRN02C.cbl
- **MDI**: 12.4（Low）
- **推奨戦略**: BigBang
- **行数**: 784 / **パラグラフ数**: 18
- **主要指標**: CC=2, GD=0.000, AD=0, ND=4

---

### COCRDLIC
- **ファイル**: COCRDLIC.cbl
- **MDI**: 11.2（Low）
- **推奨戦略**: BigBang
- **行数**: 1460 / **パラグラフ数**: 41
- **主要指標**: CC=2, GD=0.025, AD=0, ND=3

---

### COUSR00C
- **ファイル**: COUSR00C.cbl
- **MDI**: 9.7（Low）
- **推奨戦略**: BigBang
- **行数**: 696 / **パラグラフ数**: 16
- **主要指標**: CC=2, GD=0.000, AD=0, ND=4

---

### COTRN00C
- **ファイル**: COTRN00C.cbl
- **MDI**: 9.7（Low）
- **推奨戦略**: BigBang
- **行数**: 700 / **パラグラフ数**: 16
- **主要指標**: CC=2, GD=0.000, AD=0, ND=4

---

### COCRDSLC
- **ファイル**: COCRDSLC.cbl
- **MDI**: 9.6（Low）
- **推奨戦略**: BigBang
- **行数**: 888 / **パラグラフ数**: 36
- **主要指標**: CC=2, GD=0.031, AD=0, ND=2

---

### COACTVWC
- **ファイル**: COACTVWC.cbl
- **MDI**: 9.5（Low）
- **推奨戦略**: BigBang
- **行数**: 942 / **パラグラフ数**: 37
- **主要指標**: CC=2, GD=0.042, AD=0, ND=2

---

### COUSR02C
- **ファイル**: COUSR02C.cbl
- **MDI**: 9.4（Low）
- **推奨戦略**: BigBang
- **行数**: 415 / **パラグラフ数**: 11
- **主要指標**: CC=2, GD=0.000, AD=0, ND=4

---

### COUSR03C
- **ファイル**: COUSR03C.cbl
- **MDI**: 9.4（Low）
- **推奨戦略**: BigBang
- **行数**: 360 / **パラグラフ数**: 11
- **主要指標**: CC=2, GD=0.000, AD=0, ND=4

---

### COTRN01C
- **ファイル**: COTRN01C.cbl
- **MDI**: 9.3（Low）
- **推奨戦略**: BigBang
- **行数**: 331 / **パラグラフ数**: 9
- **主要指標**: CC=2, GD=0.000, AD=0, ND=4

---

### COBIL00C
- **ファイル**: COBIL00C.cbl
- **MDI**: 9.2（Low）
- **推奨戦略**: BigBang
- **行数**: 573 / **パラグラフ数**: 16
- **主要指標**: CC=2, GD=0.000, AD=0, ND=4

---

### CBACT01C
- **ファイル**: CBACT01C.cbl
- **MDI**: 8.8（Low）
- **推奨戦略**: BigBang
- **行数**: 431 / **パラグラフ数**: 16
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CORPT00C
- **ファイル**: CORPT00C.cbl
- **MDI**: 8.6（Low）
- **推奨戦略**: BigBang
- **行数**: 650 / **パラグラフ数**: 10
- **主要指標**: CC=2, GD=0.017, AD=0, ND=3

---

### CBACT03C
- **ファイル**: CBACT03C.cbl
- **MDI**: 8.4（Low）
- **推奨戦略**: BigBang
- **行数**: 179 / **パラグラフ数**: 5
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CBTRN03C
- **ファイル**: CBTRN03C.cbl
- **MDI**: 8.3（Low）
- **推奨戦略**: BigBang
- **行数**: 650 / **パラグラフ数**: 26
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CBACT02C
- **ファイル**: CBACT02C.cbl
- **MDI**: 8.2（Low）
- **推奨戦略**: BigBang
- **行数**: 179 / **パラグラフ数**: 5
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CBCUS01C
- **ファイル**: CBCUS01C.cbl
- **MDI**: 7.9（Low）
- **推奨戦略**: BigBang
- **行数**: 179 / **パラグラフ数**: 5
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CBACT04C
- **ファイル**: CBACT04C.cbl
- **MDI**: 7.7（Low）
- **推奨戦略**: BigBang
- **行数**: 653 / **パラグラフ数**: 22
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### COADM01C
- **ファイル**: COADM01C.cbl
- **MDI**: 7.7（Low）
- **推奨戦略**: BigBang
- **行数**: 289 / **パラグラフ数**: 8
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### COUSR01C
- **ファイル**: COUSR01C.cbl
- **MDI**: 7.6（Low）
- **推奨戦略**: BigBang
- **行数**: 300 / **パラグラフ数**: 9
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### COMEN01C
- **ファイル**: COMEN01C.cbl
- **MDI**: 7.5（Low）
- **推奨戦略**: BigBang
- **行数**: 309 / **パラグラフ数**: 7
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CBTRN02C
- **ファイル**: CBTRN02C.cbl
- **MDI**: 7.3（Low）
- **推奨戦略**: BigBang
- **行数**: 732 / **パラグラフ数**: 26
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CBSTM03A
- **ファイル**: CBSTM03A.CBL
- **MDI**: 7.2（Low）
- **推奨戦略**: BigBang
- **行数**: 925 / **パラグラフ数**: 25
- **主要指標**: CC=2, GD=0.017, AD=0, ND=2

---

### CBTRN01C
- **ファイル**: CBTRN01C.cbl
- **MDI**: 7.1（Low）
- **推奨戦略**: BigBang
- **行数**: 495 / **パラグラフ数**: 18
- **主要指標**: CC=2, GD=0.000, AD=0, ND=3

---

### CBEXPORT
- **ファイル**: CBEXPORT.cbl
- **MDI**: 7.0（Low）
- **推奨戦略**: BigBang
- **行数**: 583 / **パラグラフ数**: 21
- **主要指標**: CC=2, GD=0.000, AD=0, ND=2

---

### CBIMPORT
- **ファイル**: CBIMPORT.cbl
- **MDI**: 6.0（Low）
- **推奨戦略**: BigBang
- **行数**: 488 / **パラグラフ数**: 16
- **主要指標**: CC=2, GD=0.000, AD=0, ND=2

---

### COSGN00C
- **ファイル**: COSGN00C.cbl
- **MDI**: 5.7（Low）
- **推奨戦略**: BigBang
- **行数**: 261 / **パラグラフ数**: 6
- **主要指標**: CC=2, GD=0.000, AD=0, ND=2

---

### CSUTLDTC
- **ファイル**: CSUTLDTC.cbl
- **MDI**: 4.9（Low）
- **推奨戦略**: Incremental
- **行数**: 158 / **パラグラフ数**: 2
- **主要指標**: CC=1, GD=0.000, AD=0, ND=1

---

### COBSWAIT
- **ファイル**: COBSWAIT.cbl
- **MDI**: 0.5（Low）
- **推奨戦略**: BigBang
- **行数**: 42 / **パラグラフ数**: 0
- **主要指標**: CC=1, GD=0.000, AD=0, ND=0

---

