# get_transcript.py
import sys
from youtube_transcript_api import YouTubeTranscriptApi

def get_transcript(video_id: str) -> str:
    try:
        ytt_api = YouTubeTranscriptApi()
        transcript_entries = ytt_api.fetch(video_id)
        return " ".join(entry.text for entry in transcript_entries).strip()

    except Exception as e:
        print(f"ERROR: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python get_transcript.py <video_id>", file=sys.stderr)
        sys.exit(1)

    video_id = sys.argv[1]
    print(get_transcript(video_id))