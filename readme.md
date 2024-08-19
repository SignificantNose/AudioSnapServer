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

# Table of contents

- [How to launch](#how-to-launch)
    - [Manual build](#manual-build)
    - [Docker Compose application launch](#docker-compose-application-launch)
- [User manual](#user-manual)
    - [Request properties](#request-properties)
    - [Microservice response](#microservice-response)
    - [Response properties](#response-properties)
- [Notes & features](#notes--features)
    - [Considering mandatory database integration](#considering-mandatory-database-integration)
    - [HTTP requests logging](#http-requests-logging)
    - [File logging](#file-logging)
    - [Configuring HTTPS](#configuring-https)



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
        "AcoustIDKey" : "<ACOUSTID_KEY>",
        "UserAgent": "<USERAGENT>",
        "Version": "<VERSION>",
        "ContactEmail": "<EMAIL>"
    },
    "ConnectionStrings": {
        "AppDbConnectionString" : "<MYSQL_CONN_STRING>"
    }

Consequently, the corresponding environment variables that can be provided in order to launch the application:

    ASPNETCORE_ExternalAPI-Client__AcoustIDKey=<ACOUSTID_KEY>
    ASPNETCORE_ExternalAPI-Client__UserAgent=<USERAGENT>
    ASPNETCORE_ExternalAPI-Client__Version=<VERSION>
    ASPNETCORE_ExternalAPI-Client__ContactEmail=<EMAIL>

    ASPNETCORE_ConnectionStrings__AppDbConnectionString=<MYSQL_CONN_STRING>;

## Docker Compose application launch

Docker Compose launch methods are easier in comparison to launching the manually built application. Some parameters must be passed to the Compose file. There are multiple ways to do that, as described [here][Docker interpolation].

As an example, the parameters can be provided using ```.env``` file. The contents of the file are as follows:

    # external API data
    AcoustIDKey=<ACOUSTID_KEY>
    UserAgent=<USERAGENT>
    Version=<VERSION>
    ContactEmail=<EMAIL>

    # database data
    DbUser=<DBUSER>
    DbPassword=<DBPASSWORD>
    DbRootPassword=<DBROOTPASSWORD>

> Note: It is possible that MySql image will take too much time to initialize while running for the first time in a container. In that case it is possible that the healthcheck will pass, but when the audiosnap-server container sends a request to the database, it will fail, describing the issue as "It was not able to connect to any of the hosts", consequently the database will not be initialized and all the requests considering the database access will fail. There are 2 ways to solve this issue: set a larger  ```db:healthcheck:start_period``` value (e.g., 30s) and then set it to a lower value after the first launch, or restart the compose application.

# User manual
The application can only process POST requests sent to ```/snap``` endpoint. The main reason for that is that, apart from the fingerprint (which is usually a large Base64-encoded string), the microservice must receive information about what properties to return about the audio. The microservice expects to receive a JSON. An example of a minimal request that will be considered valid is the following request:

    POST https://api.audiosnap.com /snap
    Content-Type: application/json
    Accept: application/json

    {
      "fingerprint" : "AQABz0qUkZK4oOfhL-CPc4e5C_w...",
      "duration" : 100,
      "matching-rate" : 1.0,
      "release-properties" : []
    }    

This request only contains the mandatory properties. If any of these properties are missing, the request will be considered invalid.

All possible properties are described below. See [this directory](Requests/) for more examples of requests that can be sent to the microservice.

## Request properties
- `fingerprint` — Base64-encoded string, an audio fingerprint generated using Chromaprint library. Mean of recognizing the audio file, cannot be an empty string. **Mandatory**.
- `duration` — integer, audio file duration, in seconds. Mean of recognizing the audio file. **Mandatory**.
- `matching-rate` — a real number (range: [0.0; 1.0]). In case the audio track has been identified, the matching rate can vary, and not always be equal to 1.0 (meaning, 100%). This property describes the lowest matching rate — a threshold, below which the tracks will be considered unidentified. **Mandatory**.
- `release-properties` — array of strings that describes which audio properties must be included in the response in case the audio track has been successfully identified. If any of the properties are duplicated in the array, they will count as one. Any unknown strings will be considered invalid and sent back in the response in `invalid-properties` property. **Mandatory**. The possible properties are:

      album, title, artists, music-brainz-artist-id, year, aisd, isrcs, album, album-artists, album-artists-sort, artists, disc, disc-count, genres, isrcs, length, music-brainz-artist-id, music-brainz-disc-id, music-brainz-release-id, music-brainz-release-status, music-brainz-track-id, track, track-count, title
- `cover` — bool type property, determines if the response must contain the link to the cover art for the audio. The reason this property cannot be included in the `release-properties` property is that CoverArtArchive takes much longer to respond, and if the client is ready to take on the risk of acquiring the response later than without cover art, the action must be harder to perform. Not mandatory. The default value is `false`.
- `cover-size` — integer type property that defines the maximum size of the picture the client supports, in pixels. The microservice is able to return images of the following sizes: 200x200, 500x500, or 1200x1200. Not mandatory. The default value is 500.
    > Note: if the `cover-size` property is present, and the value of the `cover` property is `false`, the cover art will not be present in the response.
- `external-links` — bool type property, defines if the links to external services (like YouTube, Spotify) must be included in the response. The property cannot be included in `release-properties`, as it is not really metadata of the audiofile. Not mandatory. The default value is `false`.
- `priorities` — a JSON object, defines the choice criteria of a certain release. Each recording has multiple releases, and the client has an option to choose a certain release based on some criteria.

    The object can have two properties:
    - `release-format` — a JSON object, the names of the fields are the type of the release, and the values are the priority values. The names of the fields (meaning, the names of the release types) are listed below:
        
          album, audio-drama, audiobook, broadcast, compilation, demo, dj-mix, ep, field-recording, interview, live, mixtape, other, remix, single, soundtrack, spokenword

        Invalid name type releases are ignored, as they will never be applied to real-life scenarios. Only some of the release types can be listed. In that case the default value for any other release type that is not listed is 0.

    - `release-country` — an array of strings, defines the priority of the release in case there are multiple release types with the same priority score. The strings represent two-letter country namings. The first country that is present both in the array and in the candidate releases defines the chosen release. In case no country is found in the candidate releases, the first release is chosen.

    The property is not mandatory, and so are the fields of the JSON object the property contains. The chosen release is the first one by default.


## Microservice response 
In case there were no internal microservice errors, the response will be structured similarly to this:

    HTTP/2 200 OK
    content-type: application/json
    date: Mon, 01 Jan 2024 00:00:00 GMT
    server: Kestrel
    content-length: 914
    x-http2-stream-id: 3

    {
    "properties": {
        "album": "Mirrors",
        "title": "Suit & Tie (radio edit)",
        "artists": [
        "Justin Timberlake"
        ],
        "music-brainz-artist-id": "596ffa74-3d08-44ef-b113-765d43d12738",
        "year": "2013",
        "isrcs": "USRC11300019",
        "album-artists": [
        "Justin Timberlake"
        ],
        "album-artists-sort": [
        "Timberlake, Justin"
        ],
        "disc": 1,
        "disc-count": 1,
        "genres": null,
        "length": 268000,
        "music-brainz-disc-id": "",
        "music-brainz-release-id": "f84e3ffa-bcf2-45ca-ac4f-12a401762b35",
        "music-brainz-release-status": "Official",
        "music-brainz-track-id": "cf20c4c9-964d-405a-9878-9f83356e6ad7",
        "track": 2,
        "track-count": 2
    },
    "external-links": [
        "https://www.discogs.com/release/4341516"
    ],
    "missing-properties": [
        "image-link"
    ],
    "invalid-properties": [
        "aisd"
    ]
    }

## Response properties
- `properties` —  a JSON object, audio metadata of the recognized audio track. Each field is a value of its own. If no requested fields were found, the value is an empty JSON object.
- `external-links` — an array of strings, contains all found links to external services (like YouTube, Spotify). Present in the response only if the value of `external-links` property in the request was set to `true`.
- `image-link` — a string to the cover art of the recognized audio file. Present in the response only if the value of `cover` property in the request was set to `true`.
- `missing-properties` — an array of strings, the valid requested audio metadata names that were not found for some reason. The listed properties are not present in the `properties` JSON object.
- `invalid-properties` — an array of strings, the requested audio metadata names that are unknown to the microservice.


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

## Configuring HTTPS
The easiest way to launch the application with an HTTPS configuration is to change the launch profile. The default launch profile in manually built microservice is HTTP, as the application relies on ```Properties/launchprofile.json``` file. The launch profile can be changed using ```--launch-profile "<PROFILE_NAME>"``` flag on application launch, which'll require the service to have access to a valid HTTPS certificate (in case of the appropriate HTTPS launch profile).

While the procedure on the manually built microservice is pretty [straightforward][MSDN ASP.NET https manual], it is not that simple to implement it in a containerized microservice. The following will describe some obstacles that might appear while configuring the microservice to run as an HTTPS service in a container.
> Note considering manually built microservice: there might be some difficulties with certain linux distros that are not listed in the [article][MSDN ASP.NET https manual], e.g. various ways to trust the certificate "will not work", as the warning will appear each time the application is started, while the certificate is in fact trusted. The issue I faced was not fixed as a result — the warning remained. But it is important to note that the ```linux-dev-certs``` dotnet tool which can be used to install a trusted certificate addresses the issue, saying that the warning might still remain, and the application will still work properly.

### Docker compose HTTPS: how-to?
If you do not have a ```.pfx``` HTTPS certificate, it can be generated using this command:
    
    dotnet dev-certs https -ep <CERT_FILE_PATH> -p <CERT_CREDENTIALS>

The name of the certificate is expected to be ```audiosnap.pfx```, otherwise the certificate will not be found.

[This article][GH dotnet host https] also suggests to trust the certificate. However, in my case the certificate did not require to be trusted, and no warnings appear on the application launch. This might be different in your case, but keep in mind.

You can find a ```compose-https.yaml``` file in the repository. The default 

    docker compose up command 

will pick the ```compose.yaml``` file. That's why in order to launch the HTTPS-configured compose file you have to use the following command:

    docker compose -f compose-https.yaml up

Also, some additonal parameters must be provided to the compose file. They can be provided the same way as described [here](#docker-compose-application-launch). The parameters are:

    certDirPath=<PATH_TO_DIR_WITH_CERTIFICATE>
    certCredentials=<CERTIFICATE_CREDENTIALS>


### What could possibly go wrong?
The microservice might not have access to the certificate. This is possible when the access permissions of the mounted directory with the certificate (or the certificate itself) are restrictive. Make sure users have the access to read the file and the directory.

#### OR

The ```compose-https.yaml``` file can be changed, so that the target mount directory is a directory that is not accessible by a user in the container (also, in that case the environment variable that is responsible for storing the certificate location must be changed in order to correspond to the relevant certificate path). The provided directory path IS accessible, it should not be inaccessible. But also keep in mind that it is possible.

# Conclusion
Considering this repository, it can be said that, as a result, a microservice has been created that is flexible due to the features ASP.NET Core provides. The microservice can also be modified to be independent of the external APIs, as the communication interface that these APIs follow can be considered all-purpose, as it is easy to follow.


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
[MSDN ASP.NET https manual]: <https://learn.microsoft.com/en-en/aspnet/core/security/enforcing-ssl?view=aspnetcore-8.0>

[GH dotnet host https]: <https://github.com/dotnet/dotnet-docker/blob/main/samples/host-aspnetcore-https.md>