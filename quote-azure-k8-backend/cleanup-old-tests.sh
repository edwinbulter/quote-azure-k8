#!/bin/bash
# cleanup-old-tests.sh

echo "=== Finding Old Test Resources ==="
# Find all test groups except the current one
CURRENT_GROUP=$(az group list --query "[?contains(name, 'test-') && !contains(name, 'MC_')].name" -o tsv | sort -r | head -1)

echo "Current active group: $CURRENT_GROUP"
echo ""

echo "=== Old Test Groups to Delete ==="
OLD_GROUPS=$(az group list --query "[?contains(name, 'test-') && !contains(name, 'MC_')].name" -o tsv | sort -r | tail -n +2)

if [ ! -z "$OLD_GROUPS" ]; then
    echo "Found old groups:"
    echo "$OLD_GROUPS"
    echo ""
    echo "Deleting old test groups..."
    
    for GROUP in $OLD_GROUPS; do
        echo "Deleting: $GROUP"
        az group delete --name $GROUP --yes --no-wait
    done
    
    echo ""
    echo "=== Cleanup Started! ==="
    echo "Old groups are being deleted in background"
    echo "Current active group: $CURRENT_GROUP"
else
    echo "No old test groups found"
fi
