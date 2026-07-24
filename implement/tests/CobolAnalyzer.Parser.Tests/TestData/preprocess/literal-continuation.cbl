000100 IDENTIFICATION DIVISION.                                         
000200 PROGRAM-ID. LITCONT.                                             
000300 DATA DIVISION.                                                   
000400 WORKING-STORAGE SECTION.                                         
000500 01 WS-HTML PIC X(80) VALUE '<td style="f:12px Segoe UI,          
000600-    'sans-serif;">'.                                             
000700 PROCEDURE DIVISION.                                              
000800 MAIN-PARA.                                                       
000900     DISPLAY WS-HTML.                                             
001000     STOP RUN.                                                    
