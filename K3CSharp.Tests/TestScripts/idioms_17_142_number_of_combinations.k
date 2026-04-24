fac:{[n]:[n>1; n * _f[n-1];1]};bin:{[n;k]fac[n]%fac[n-k]*fac[k]};bin[10;4]
