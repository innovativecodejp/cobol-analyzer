000100 IDENTIFICATION DIVISION.                                         IDJUNK73
000200 PROGRAM-ID. FIXEDFORM.                                           PIDJUNK7
000300*THIS LINE IS A COMMENT (COL7 STAR)                               
000400/THIS LINE IS A COMMENT (COL7 SLASH)                              
000500 ENVIRONMENT DIVISION.                                            
000600 DATA DIVISION.                                                   
000700 WORKING-STORAGE SECTION.                                         
000800 01 WS-MESSAGE PIC X(20) VALUE 'HELLO                             
000900-    WORLD'.                                                      
001000 PROCEDURE DIVISION.                                              
001100 MAIN-PARA.                                                       
001200     DISPLAY WS-MESSAGE.                                          STOPJUNK
001300     STOP RUN.                                                    
