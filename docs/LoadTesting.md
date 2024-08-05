## Load testing with k6
Before running tests you should mock external dependencies like:
- AltinnAuthorization by setting the function CheckUserAccess to return true
- AltinnRegisterService to return a string 
- AltinnResourceRegister to return a ResourceEntity
- Use the ConsoleLogEventBus instead of AltinnEventBus

Constants: 
- BASE_URL; enviroment to test. 
- TOKENS: tokens for a service owner(TOKENS.DUMMY_SERVICE_OWNER_TOKEN) and a sender(TOKENS.DUMMY_SENDER_TOKEN), which can be found in postman(Authenticate as Sender/serviceOwner) in the Authenticator folder. 

k6 option variables: 
- VUs: How many virtual users running tests at the same time. 
- iterations: how many tests TOTAL should be completed. vus/iterations=test per vus. 0 means infinite iterations for as long as the test will run. 
- httpDebug: full/summary. Outputs infomration about http requests and responses
- duration: How long the test should be running. The test also adds a 30 seconds gracefull stop period on top of this. 

We run load tests using k6. To run without installing k6 you can use docker compose(base url has to be http://host.docker.internal:5096):
```docker compose -f docker-compose-loadtest.yml up k6-test``` 

if you have k6 installed locally, you can run it by using the following command: 
```"k6 run test.js"```
