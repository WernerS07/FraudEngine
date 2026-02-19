-= Fraud Engine Overview =- 

Main Componenets / Micro-services:
    
    - Postgres Databases: Keycloak (storing credentials), Transactions DB (for all transactions)
    
    - KafkaConsumer: Consumer service to consume and process all transactions. Includes a fraudcheck services to 
                     apply a set of fraud rules on a transactional level. Transactions are tagged with a true or false for potential fraud and then stored in the transactions DB in a Records
                     table. 
    
    - FraudEngineService: Main API for the Fraud Engine. Extensive API for querying records, based on several
                          criteria's. Also allows for the update of the fraud status on existing records, after auditing has been peformed. The Api would rarely need to be scaled as minimal
                          computation happens. But if needed, it can be scaled based on traffic.
    
    - Gateway: A YARP service that handles all incoming traffic on localhost:80/ and routes it to either the       
               MockDataProducer or the FraudEngineService. Applies authentication and authorization to routes enabling role based access control before traffic is routed to downstream services.
               Capability to enable load balancing if needed in a production setting. 
    
    - KeyCloak: Used for issuing JWT tokens and user registration. A config file has been included that will create 
                the realm with the existing demo clients on startup. The passwords can be found in the included .env file. In a production setting this will be injected via a vault. 
    
    - MockDataProducer: A service to create mock transactions for the purpose of demoing. In a real world scenario 
                        this would not be included, instead your actual transactions would be posted to the kafka topic instead of the mock data. This service includes a kafka producer to post
                        all created data to the kafka transactions topic. 
    
    - Redis: A redis cache for increased performance on API calls. Also enables to consumer to perform specific
             checks that look at recent histories of a client. For example, if a client has multiple transactions in a short period of time. By using a cache this can be done without unneccesary
             queries to the DB. In a production setting the cache can be scalled based on needs. 
    
    - Shared: Includes database migrations and redis cache functions.
    
Files needed to run locally:

    - If running on a Capitec device. A zscaler root certificate needs to be in the certs/ folder. If it is 
      missing the docker files will fail when attemping to download nuget packages.   
    - A demo .env is included that has all the neccesary credentials to build and run the Fraud Engine  
      However, for production, secrets should be kept in a vault and injected at run time. 
    - Also included is a keycloak-config.json file that has all the realm, client and role settings to run the   
      demo. In a production setting, keycloak would be run with replicas to provide high availability. With a more secure admin panel, using AD groups instead of just the basic password provided
      for demo purposes.

Testing: 

    - To test the Fraud Engine, a .html file is provided in the root folder that includes all the api requests.  
      Included in this is also 2 clients with a user and pass, to test both of the access roles. Each endpoint is documented in this file. 

Additional requirements:

    - Docker needs to be installed on the system that will be running the docker-compose file
      When in the root folder of the project you can simply run docker compose up -d

