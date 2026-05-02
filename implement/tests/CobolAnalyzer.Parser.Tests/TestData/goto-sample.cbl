       IDENTIFICATION DIVISION.
       PROGRAM-ID. GOTO-SAMPLE.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-FLAG PIC 9 VALUE 0.
       PROCEDURE DIVISION.
       MAIN-PARA.
           IF WS-FLAG = 1
               GO TO PROCESS-PARA
           END-IF.
           GO TO END-PARA.
       PROCESS-PARA.
           PERFORM CALC-PARA THRU CALC-END-PARA.
           GO TO END-PARA.
       CALC-PARA.
           MOVE 1 TO WS-FLAG.
       CALC-END-PARA.
           MOVE 0 TO WS-FLAG.
       END-PARA.
           STOP RUN.
