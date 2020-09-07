import logging
import azure.functions as func
import json
from youtube_transcript_api import YouTubeTranscriptApi

def main(req: func.HttpRequest) -> func.HttpResponse:
    logging.info('Python HTTP trigger function processed a request.')
    
    videoid = req.params.get('videoid')

    if videoid:
        data = YouTubeTranscriptApi.get_transcript(videoid, languages=['es'])
        transcription = json.dumps(data)
        return transcription

    else:
        return func.HttpResponse(
             "Pass a video id in the query string",
             status_code=200
        )
