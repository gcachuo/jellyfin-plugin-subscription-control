#!/bin/bash

# Script para ejecutar pruebas de regresión end-to-end con API real
# Estas pruebas verifican el sistema completo contra la API de producción

echo "🔴 Running End-to-End Regression Tests with REAL API"
echo ""
echo "⚠️  WARNING: These tests will make REAL calls to the production API"
echo ""

# Configurar URL de la API
API_URL="${EASYMOVIE_API_URL:-https://easymovie.lat/api/subscription.php}"

echo "API URL: $API_URL"
echo ""

# Preguntar confirmación
read -p "Continue with real API tests? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]
then
    echo "❌ Aborted"
    exit 1
fi

echo ""
echo "🧪 Running regression tests..."
echo ""

# Exportar variable de entorno y ejecutar tests
export EASYMOVIE_API_URL="$API_URL"

# Ejecutar solo tests de regresión end-to-end
dotnet test EasyMovie.Plugin.IntegrationTests \
    --filter "FullyQualifiedName~EndToEndRegressionTests" \
    --logger "console;verbosity=normal"

TEST_RESULT=$?

echo ""
if [ $TEST_RESULT -eq 0 ]; then
    echo "✅ All end-to-end regression tests passed!"
    echo ""
    echo "This confirms:"
    echo "  ✓ Real API is working correctly"
    echo "  ✓ Trial user restrictions are enforced"
    echo "  ✓ API contract is stable"
    echo "  ✓ Data quality is good"
else
    echo "❌ Some regression tests failed!"
    echo ""
    echo "This indicates:"
    echo "  ✗ API may have changed"
    echo "  ✗ Production data may be inconsistent"
    echo "  ✗ Network issues"
    echo ""
    echo "Review the test output above for details."
fi

exit $TEST_RESULT
