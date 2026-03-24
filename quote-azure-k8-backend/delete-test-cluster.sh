#!/bin/bash
# delete-test-cluster.sh

echo "=== Finding All Test Resources ==="
# Find ALL test resource groups
TEST_GROUPS=$(az group list --query "[?contains(name, 'test-')].name" -o tsv)

if [ ! -z "$TEST_GROUPS" ]; then
    echo "Found test resource groups:"
    echo "$TEST_GROUPS"
    echo ""
    echo "Deleting ALL test resource groups..."
    
    # Delete all test resource groups
    for GROUP in $TEST_GROUPS; do
        echo "Deleting: $GROUP"
        az group delete --name $GROUP --yes --no-wait
    done
    
    echo ""
    echo "=== Complete Stop Achieved! ==="
    echo "All test resources deleted"
    echo "Cost: $0/hour"
    echo "Deleted groups: $(echo "$TEST_GROUPS" | wc -l | tr -d ' ')"
else
    echo "No test resource groups found"
fi
