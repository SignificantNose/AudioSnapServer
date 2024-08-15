# AudioSnap — audio metadata at your fingertips

AudioSnap is a project that strives to make audio metadata fetching easier by utilizing audio fingerprinting algorithms and making use of extensive audio metadata storages. The primary goal of this project was to get familiar with audio recognition algorithms and techniques.  

AudioSnap project is composed of 3 key components:
- AudioSnap microservice — the current repository
- [AudioSnap client] — utilizes this microservice to fetch audio metadata
- [Chromaprint library] — included in both the microservice and the client

The inspiration for the project comes from [this article][chromaprint article], that goes deep into audio recognition algorithms that formed the basis of the following web APIs our project relies on:
- [AcoustID] — recognize audio by its fingerprint
- [MusicBrainz] — fetch audio metadata by its ID, relies on AcoustID
- [CoverArtArchive] — fetch link to audio cover art, relied on MusicBrainz/AcoustID

## Disclaimer

> Although **there ARE solutions to the problem** provided by AcoustID and MusicBrainz themselves (e.g. [MusicBrainz Picard]), our project's goal was to — again — get familiar with the audio fingerprinting algorithms and build something that can be used as a convenient mean of providing metadata to audio files, analagous to solutions provided by MusicBrainz and AcoustID.  
Our goal was not to build a counterpart OR competitor to these solutions by any means, as our solution strongly relies on their services and their audio fingerprinting algorithms. Neither was our goal to monetise the application, even though it is allowed on a paid basis.  
Same goes to the rewritten [open-source chromaprint library][original chromaprint library], which is basically the implementation of the audio fingerprinting algorithms described in [this article][chromaprint article]. The goal was to get familiar with the algorithms, and we believe that the best way to learn is to practise.



# How to launch
The web service can be launched with 2 ways:
- Manually build the project
- Launch Docker Compose application

Either way, some configuration parameters must be provided. In order to communicate with external APIs, following parameters must be provided:
- AcoustID API Key - required by AcoustID to fulfill the requests
- User Agent - required by MusicBrainz
- Application version - required by MusicBrainz
- Contact Email - required by MusicBrainz

Your application must be registered [here][AcoustID register application] in order for AcoustID to fulfill your requests. More information on parameters for MusicBrainz can be found [here][MusicBrainz UserAgent strings].

> WARNING: the API key is visible in logs, as every HTTP request is logged. The API key goes in a GET request, as the AcoustID service requires. This cannot be avoided because of the fact that all requests to AcoustID are GET requests. Please consider this fact and take additional precautions against the machine being compromised, so that the logs are not visible to third-parties. Also, read the note below considering the application secrets.

> Note: in real-life scenarios the API key should not be stored as an environment variable, in case no other measures were taken (e.g., encryption of the variable value). Alternatives to application secrets can be found in [this MSDN article][MSDN ASP.NET secrets].

The application also requires to have a connection string parameter installed in order to be able to communicate with a MySql database. The database integration is mandatory. More on this issue can be found in the [notes](#considering-mandatory-database-integration).

## Manual build

In order to build the project you'll have to add a reference to [our Chromaprint library][Chromaprint library] package. Once the library project has been built, you must provide the compiled library to the appropriate location. The location where the current project expects to find the compiled ```.dll``` file is provided in ```AudioSnapServer.csproj```, and is currently equal to: ```./Packages/Chromaprint.dll```.

After building the project, the binary can be successfully launched, but the web service will fail to launch, as the configuration parameters described above are missing. In order to provide them, multiple approaches can be taken, according to a documentation [article][MSDN ASP.NET config] considering configuration in ASP.NET.  
The safer option is to incude these parameters as environment variables.  

Required configuration parameters are as follows:

    "ExternalAPI-Client": {
        "AcoustIDKey" : "key",
        "UserAgent": "useragent",
        "Version": "version",
        "ContactEmail": "email"
    },
    "ConnectionStrings": {
        "AppDbConnectionString" : "SERVER=server;DATABASE=database;USER=user;PASSWORD=password;"
    }

Consequently, the corresponding environment variables that can be provided in order to launch the application:

    ASPNETCORE_ExternalAPI-Client__AcoustIDKey=key
    ASPNETCORE_ExternalAPI-Client__UserAgent=useragent
    ASPNETCORE_ExternalAPI-Client__Version=version
    ASPNETCORE_ExternalAPI-Client__ContactEmail=email

    ASPNETCORE_ConnectionStrings__AppDbConnectionString=SERVER=server;DATABASE=database;USER=user;PASSWORD=password;

## Docker Compose application launch

Docker Compose launch methods are easier in comparison to manual launch methods. Some parameters must be passed to the Compose file. There are multiple ways to do that, as described [here][Docker interpolation].

As an example, the parameters can be provided using ```.env``` file. The contents of the file are as follows:

    # external API data
    AcoustIDKey=key
    UserAgent=useragent
    Version=version
    ContactEmail=email

    # database data
    DbUser=user
    DbPassword=password
    DbRootPassword=password2

> Note: It is possible that MySql image will take too much time to initialize while running for the first time in a container. In that case it is possible that the healthcheck will pass, but when the audiosnap-server container sends a request to the database, it will fail, describing the issue as "It was not able to connect to any of the hosts", consequently the database will not be initialized and all the requests considering the database access will fail. There are 2 ways to solve this issue: set a larger  ```db:healthcheck:start_period``` value (e.g., 30s) and then set it to a lower value after the first launch, or restart the compose application.

# Notes & features
## Considering mandatory database integration
The application expects to be able to connect to the database and initialize it. This approach excludes the fact that the microservice's functionality can be duplicated on any client, therefore making the microservice useful in terms of storage feature. That way the client doesn't have to store anything, and in fact, that's how it should be.  

However, without the database the microservice will be unresponsive. Unfortunately, this was not taken into account while developing the microservice. It was a mistake, as in my opinion the inability to connect to a database **must not** be the reason to fail to provide the client with the requested data.

## HTTP requests logging
A custom HTTP requests logging has been introduced to the microservice. The main reason foe this is that the outgoing requests to the external microservices (mainly, AcoustID) are **large** because of the fingerprint size, which goes in a GET request. Whenever a request occurs, it is logged. The default HTTP client logger will log the stages of the request, each time duplicating the URL, which makes it difficult to navigate through logs when there are multiple identical long URLs. The custom HTTP logger mitigates this issue by displaying the URL once.

One more important reason for custom implementation of the logger is the display of the time the request took to be processed, which the default HTTP logger implementation lacks. This was considered crucial, as it allowed to find out which services were slower and, therefore, **longer to respond**. This information can be useful later in order to receive responses from certain services faster, if the response time doesn't change (meaning, it is not related to the service's workload).

## File logging
The microservice allows to log the actions in 2 ways:
- Console logging (always enabled)
- File logging (disabled by default)

In order to enable file logging, a configuration parameter must be provided:
    
    "Logging": {
        "DateFile": {
            "LogDirPath": "<PATH_TO_LOG_DIRECTORY>"
        }
    }

The configuration parameter can be provided the same way as the parameters described in [manual build](#manual-build). 

> E.g. in order to provide the configuration parameter as an environment variable, the name of the variable can be the following:
>    
>     ASPNETCORE_Logging__DateFile__LogDirPath=<PATH_TO_LOG_DIRECTORY>

The microservice will try to create the directory provided in the configuration parameter, in case it doesn't exist, and then create the file there. The name of the file will reflect the date and time the microservice has been started on. Each new launch of the microservice will create a new log file.

[AudioSnap client]: <https://github.com/0TheThing0/AvaloniaAudioSnap>
[Chromaprint library]: <https://github.com/0TheThing0/Chromaprint_lib>
[Original chromaprint library]: <https://github.com/acoustid/chromaprint/tree/master>
[Chromaprint article]: <https://oxygene.sk/2011/01/how-does-chromaprint-work/>

[AcoustID]: <https://acoustid.org/>
[AcoustID register application]: <https://acoustid.org/new-application>

[MusicBrainz]: <https://musicbrainz.org/>
[MusicBrainz UserAgent strings]: <https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting#Provide_meaningful_User-Agent_strings>

[CoverArtArchive]: <https://coverartarchive.org/>

[MusicBrainz Picard]: <https://picard.musicbrainz.org/>

[Docker interpolation]: <https://docs.docker.com/compose/environment-variables/variable-interpolation/#ways-to-set-variables-with-interpolation>

[MSDN ASP.NET config]: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0>
[MSDN ASP.NET secrets]: <https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-8.0>
