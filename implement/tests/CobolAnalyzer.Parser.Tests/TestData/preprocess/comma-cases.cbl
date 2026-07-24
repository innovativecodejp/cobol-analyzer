000100 CALL 'X' USING WS-A, WS-B                                        
000200 05 WS-PIC PIC ZZ,ZZ9.                                            
000300 05 WS-DEC PIC 9V9 VALUE 1,5.                                     
000400 05 WS-LIT PIC X(10) VALUE 'a, b'.                                
000500 05 WS-HTM PIC X(30) VALUE 'Segoe UI,sans-serif'.                 
000600 MOVE == A, B == TO WS-A                                          
000700 MOVE TBL(I,J) TO WS-A                                            
