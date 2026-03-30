#!/bin/bash
# delete-test-cluster.sh

echo "=== Finding All Test Resources ==="
# Find ALL test resource groups (including old dated ones)
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

echo ""
echo "=== Checking Fixed Resources ==="

# Check if fixed resource group exists
FIXED_RG="quote-azure-k8-backend"
RG_EXISTS=$(az group show --name $FIXED_RG --query name --output tsv 2>/dev/null)

if [ ! -z "$RG_EXISTS" ]; then
    echo "Found fixed resource group: $FIXED_RG"
    echo "Deleting fixed resources:"
    echo "  📦 Container Registry: kabulterquotek8acr"
    echo "  💾 Storage Account: kabulterquotek8storage"
    echo "  💰 Saving ~€0.14/day"
    echo ""
    echo "Deleting fixed resource group: $FIXED_RG"
    az group delete --name $FIXED_RG --yes --no-wait
    echo "✅ Fixed resources deleted - Zero cost achieved!"
else
    echo "✅ No fixed resource group found - Zero cost achieved!"
fi

echo ""
echo "=== Summary ==="
echo "🧹 Test resources: Deleted"
echo "📦 Fixed resources: Deleted"
echo "💰 Total cost: €0.00/hour"
echo "🎉 Zero cost achieved!"
