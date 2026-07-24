000100 IDENTIFICATION DIVISION.                                         
000200 PROGRAM-ID. COMMASEP.                                            
000300 DATA DIVISION.                                                   
000400 WORKING-STORAGE SECTION.                                         
000500 01 WS-A PIC X(5).                                                
000600 01 WS-B PIC X(5).                                                
000700 01 WS-OUT PIC X(20).                                             
000800 PROCEDURE DIVISION.                                              
000900 MAIN-PARA.                                                       
001000     CALL 'SUB' USING WS-A, WS-B.                                 
001100     STRING WS-A,                                                 
001200            WS-B                                                  
001300         DELIMITED BY SIZE INTO WS-OUT.                           
001400     STOP RUN.                                                    
