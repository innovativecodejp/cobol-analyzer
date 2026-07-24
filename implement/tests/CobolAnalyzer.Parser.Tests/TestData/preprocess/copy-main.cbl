000100 IDENTIFICATION DIVISION.                                         
000200 PROGRAM-ID. COPYTEST.                                            
000300 ENVIRONMENT DIVISION.                                            
000400 DATA DIVISION.                                                   
000500 WORKING-STORAGE SECTION.                                         
000600 COPY CUSTREC.                                                    
000700 COPY MISSINGBOOK.                                                
000800 PROCEDURE DIVISION.                                              
000900 MAIN-PARA.                                                       
001000     DISPLAY CUST-NAME.                                           
001100     STOP RUN.                                                    
