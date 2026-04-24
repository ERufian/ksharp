fac:{[n]:[n>1; n * _f[n-1];1]};bin:{[n;k]fac[n]%fac[n-k]*fac[k]};pn:{[n;k]fac[k]*bin[n;k]};pn[5;3]
