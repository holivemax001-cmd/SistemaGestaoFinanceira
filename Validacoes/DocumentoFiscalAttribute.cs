using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SistemaGestaoFinanceira.Validacoes;

public sealed class DocumentoFiscalAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string documento)
            return false;

        var numeros = new string(documento.Where(char.IsDigit).ToArray());

        if (numeros.Length == 11)
            return ValidarCpf(numeros);

        if (numeros.Length == 14)
            return ValidarCnpj(numeros);

        return false;
    }

    private static bool ValidarCpf(string cpf)
    {
        if (cpf.Distinct().Count() == 1)
            return false;

        int[] pesos1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] pesos2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        int soma = pesos1.Select((peso, i) => (cpf[i] - '0') * peso).Sum();
        int resto = soma % 11;
        int digito1 = resto < 2 ? 0 : 11 - resto;

        soma = pesos2.Select((peso, i) => (cpf[i] - '0') * peso).Sum();
        resto = soma % 11;
        int digito2 = resto < 2 ? 0 : 11 - resto;

        return cpf[9] - '0' == digito1 &&
               cpf[10] - '0' == digito2;
    }

    private static bool ValidarCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1)
            return false;

        int[] pesos1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] pesos2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        int soma = pesos1.Select((peso, i) => (cnpj[i] - '0') * peso).Sum();
        int resto = soma % 11;
        int digito1 = resto < 2 ? 0 : 11 - resto;

        soma = pesos2.Select((peso, i) => (cnpj[i] - '0') * peso).Sum();
        resto = soma % 11;
        int digito2 = resto < 2 ? 0 : 11 - resto;

        return cnpj[12] - '0' == digito1 &&
               cnpj[13] - '0' == digito2;
    }
}
