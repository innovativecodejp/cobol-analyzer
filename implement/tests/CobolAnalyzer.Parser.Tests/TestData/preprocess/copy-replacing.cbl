000100 IDENTIFICATION DIVISION.                                         
000200 PROGRAM-ID. REPLTEST.                                            
000300 DATA DIVISION.                                                   
000400 WORKING-STORAGE SECTION.                                         
000500 COPY CUSTREC REPLACING ==CUST== BY ==CLIENT==.                   
000600 PROCEDURE DIVISION.                                              
000700 MAIN-PARA.                                                       
000800     STOP RUN.                                                    
