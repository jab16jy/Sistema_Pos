#!/usr/bin/env bash
# Sistema POS — WhatsApp send script
# Args: $1=phone $2=message $3=attachment_path (optional)
#
# Configure your WhatsApp bridge below. Examples:
#   npx wppconnect-cli send "$1" "$2" "$3"
#   node /opt/wa-bridge/send.js "$1" "$2" "$3"
#
# This script is called by WhatsAppScriptService.cs
# Return exit code 0 on success, non-zero on failure.

PHONE="$1"
MESSAGE="$2"
ATTACHMENT="$3"

if [ -z "$PHONE" ]; then
    echo "Error: phone number required" >&2
    exit 1
fi

# Replace this with your actual WhatsApp bridge command:
echo "WhatsApp bridge not configured. Would send to: $PHONE"
echo "Message: $MESSAGE"
[ -n "$ATTACHMENT" ] && echo "Attachment: $ATTACHMENT"

# Exit 0 so the log shows attempted (bridge not configured)
exit 0
