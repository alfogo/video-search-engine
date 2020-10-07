import logging
import azure.functions as func
import json
from youtube_transcript_api import YouTubeTranscriptApi
from iso_language_codes import *

def main(req: func.HttpRequest) -> func.HttpResponse:
    videoid = req.params.get('videoid')
    code = req.params.get('code')

    languages = language_dictionary().keys()

    if videoid and code in languages:
        data = YouTubeTranscriptApi.get_transcript(videoid, languages=["{0}".format(code)])
        transcription = json.dumps(data)
        return transcription
    else:
        return func.HttpResponse(
             "Pass a video id and language code in the query string",
             status_code=200
        )
