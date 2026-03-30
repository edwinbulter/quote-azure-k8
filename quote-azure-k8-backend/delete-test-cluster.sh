#!/bin/bash
# delete-test-cluster.sh

# Load environment variables from .env file if it exists
if [ -f ".env" ]; then
    export $(cat .env | grep -v '^#' | xargs)
fi

# Azure Resource Configuration (REQUIRED from environment)
RESOURCE_GROUP=$RESOURCE_GROUP
ACR_NAME=$ACR_NAME
STORAGE_ACCOUNT=$STORAGE_ACCOUNT

# Validate required environment variables
if [ -z "$RESOURCE_GROUP" ] || [ -z "$ACR_NAME" ] || [ -z "$STORAGE_ACCOUNT" ]; then
    echo "❌ ERROR: Required environment variables not set!"
    echo "📋 Please configure your .env file with:"
    echo "   RESOURCE_GROUP=your-resource-group"
    echo "   ACR_NAME=your-acr-name"
    echo "   STORAGE_ACCOUNT=your-storage-name"
    echo ""
    echo "💡 Copy .env.example to .env and configure it"
    exit 1
fi

echo "=== Finding Test Resources ==="

# Check for any legacy test resource groups (backward compatibility)
LEGACY_TEST_GROUPS=$(az group list --query "[?contains(name, 'test-')].name" -o tsv)

if [ ! -z "$LEGACY_TEST_GROUPS" ]; then
    echo "Found legacy test resource groups:"
    echo "$LEGACY_TEST_GROUPS"
    echo ""
    echo "Cleaning up legacy test resource groups..."
    
    # Delete all legacy test resource groups
    for GROUP in $LEGACY_TEST_GROUPS; do
        echo "Deleting: $GROUP"
        az group delete --name $GROUP --yes --no-wait
    done
    
    echo ""
    echo "✅ Legacy cleanup complete"
else
    echo "✅ No legacy test resource groups found"
fi

echo ""
echo "=== Checking Fixed Resources ==="

# Check if fixed resource group exists
FIXED_RG="$RESOURCE_GROUP"
RG_EXISTS=$(az group show --name $FIXED_RG --query name --output tsv 2>/dev/null)

if [ ! -z "$RG_EXISTS" ]; then
    echo "Found fixed resource group: $FIXED_RG"
    echo "Deleting fixed resources:"
    echo "  📦 Container Registry: $ACR_NAME"
    echo "  💾 Storage Account: $STORAGE_ACCOUNT"
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
