000100 IDENTIFICATION DIVISION.                                         
000200 PROGRAM-ID. EXECTEST.                                            
000300 ENVIRONMENT DIVISION.                                            
000400 DATA DIVISION.                                                   
000500 WORKING-STORAGE SECTION.                                         
000600 01 WS-MSG PIC X(10).                                             
000700 PROCEDURE DIVISION.                                              
000800 MAIN-PARA.                                                       
000900     EXEC CICS SEND                                               
001000               FROM (WS-MSG)                                      
001100     END-EXEC.                                                    
001200     EXEC SQL SELECT 1 END-EXEC.                                  
001300     STOP RUN.                                                    
