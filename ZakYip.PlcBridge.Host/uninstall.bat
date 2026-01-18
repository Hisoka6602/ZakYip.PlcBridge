set serviceName=ZakYip.PlcBridge.Host

sc stop   %serviceName% 
sc delete %serviceName% 

pause